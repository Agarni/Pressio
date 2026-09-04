# Pressio — Design de Sincronização (nuvem — Supabase)

## 1. Objetivo

Permitir usar o Pressio em mais de um dispositivo (celular/computador) com os mesmos dados, e que **cada pessoa tenha os próprios dados** (múltiplos usuários). A solução usa **Supabase** (plano gratuito) como "nuvem": o app mantém o **SQLite local como fonte de verdade** e sincroniza **o snapshot do usuário logado**.

**Abordagem:** **Auth por e-mail + senha** (Supabase Auth) e **Row Level Security (RLS)** — cada usuário só lê/escreve a PRÓPRIA linha (`auth.uid() = user_id`). O botão **"Sincronizar agora"** busca, **mescla (last-write-wins)** e grava o snapshot do usuário.

> **Por que não mais pasta/iCloud/OneDrive:** o seletor de pastas do iOS/Avalonia não funciona de forma confiável com provedores de nuvem (permissão/security-scoped). A UI de pasta foi **removida**; a nuvem (Supabase) funciona igual em qualquer plataforma.

---

## 2. Modelo de dados na nuvem

Tabela `pressio_sync` no Supabase (criada no SQL Editor):

```sql
create table if not exists pressio_sync (
  user_id  uuid primary key references auth.users(id) on delete cascade,
  snapshot text not null default '',
  updated_at timestamptz not null default now()
);
alter table pressio_sync enable row level security;
create policy "select own" on pressio_sync for select using (auth.uid() = user_id);
create policy "insert own" on pressio_sync for insert with check (auth.uid() = user_id);
create policy "update own" on pressio_sync for update using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "delete own" on pressio_sync for delete using (auth.uid() = user_id);
```

- Uma **linha por usuário** (`user_id`), contendo o **snapshot JSON** (`snapshot`).
- **RLS** garante que ninguém acesse dados alheios, mesmo com a `anon` key pública.

---

## 3. Snapshot de sincronia (`pressio-sync.json`)

```jsonc
{
  "formatVersion": 1,
  "deviceId": "<guid desta máquina>",
  "exportedAt": "2026-09-03T12:00:00Z",
  "patients": [ { "syncId": "<guid>", "name": "João", "updatedAt": "…", "deleted": false } ],
  "measurements": [ { "syncId": "<guid>", "patientSyncId": "<guid>", "systolic": 130, "diastolic": 80,
                       "medication": 1, "context": 19, "heartRate": 72, "atRest": true,
                       "arm": "Right", "position": "Seated", "measuredAt": "…", "notes": "…",
                       "updatedAt": "…", "deleted": false } ],
  "reminders": [ { "syncId": "<guid>", "time": "08:00:00", "days": 127, "enabled": true, "note": "…", "updatedAt": "…", "deleted": false } ],
  "settings": { "Theme": { "value": "Escuro", "updatedAt": "…" } }
}
```

### Por que `syncId` (Guid) em vez do id inteiro do SQLite?
IDs inteiros autoincrementais são **locais** e **colidem** entre dispositivos. A sincronia exige uma **chave globalmente única** (`Guid`). O id inteiro é a PK interna; o `syncId` é a identidade para mesclar. Relações (medição→paciente) usam `patientSyncId`.

### Tombstones
Exclusões são `deleted: true` (não remoção), para "viajarem" entre dispositivos sem ressuscitar. Registros antigos `deleted: true` podem ser podados futuramente (compactação).

---

## 4. Estratégia de mesclagem (merge)

**Última escrita vence por registro** (last-write-wins por entidade), usando `updatedAt`:

```
para cada entidade em { pacientes, medições, lembretes, configurações }:
    local x remoto por syncId
      - só em um lado                  -> inclui
      - nos dois                       -> vence o de `updatedAt` mais recente; `deleted:true` vence
```

- **Granularidade:** por registro (não "arquivo ganha") — evita apagar dados de um dispositivo por uma escrita no outro.
- **Conflito simultâneo:** vence o mais recente; para uso pessoal é aceitável.
- **`deviceId`:** apenas diagnóstico/auditoria (não é base da resolução).

> `updatedAt` é UTC. Se um relógio estiver adiantado, tende a "vencer" — ok para MVP. (Evolução futura: *vector clock*.)

---

## 5. Núcleo de sincronia (componentes)

- **`SyncService`** (`Pressio/Services`): monta o snapshot local, **mescla** (LWW) e **aplica** no banco (upsert por `syncId` + tombstones). `ApplyRemote(json)` / `Serialize(snapshot)`.
- **`SyncStore`** (`Pressio/Services`): serializa/desserializa `pressio-sync.json` (System.Text.Json, camelCase + enums string, tolerante a versão).
- **`SupabaseClient`** (`Pressio/Services`): REST via `HttpClient` — **Auth** (`/auth/v1/signup`, `/auth/v1/token?grant_type=password|refresh_token`) e **snapshot** (PostgREST `GET/POST /rest/v1/pressio_sync` por `user_id`, `Prefer: resolution=merge-duplicates`). Sessão (access/refresh token + user + email) serializada em `Settings`.
- **`SettingsRepository`**: guarda URL + `anon` key (padrão embutido — pública/segura com RLS), sessão do usuário, `AuthEmail` (e-mail lembrado), `LastPatientId`.
- **`MainViewModel`**: `SyncNow` → busca o snapshot do usuário, `ApplyRemote` (mescla), grava de volta; recarrega pacientes/medições/lembretes e reagenda notificações. Auth em **Configurações → Sincronização** (e-mail, senha, "Criar conta"/"Entrar"/"Sair").

> O banco local continua 100% funcional **offline**; a UI não depende de rede. O app abre no último paciente acessado.

---

## 6. Segurança (importante)

- **`anon` key** é pública e **segura para embutir** no app — por causa do **RLS**. **NUNCA** use/commit a **`service_role`** (ignora as permissões).
- A sessão (tokens) fica no **Settings local** (machine-local); a **senha** não é persistida.
- Dados de saúde: proteção por **Auth (e-mail+senha, com confirmação)** + **RLS** por usuário. (Magic link ficou de lado por exigir deep-link por plataforma.)

---

## 7. Alternativas consideradas (não usadas)

| Opção | Motivo da decisão |
|---|---|
| **Pasta de nuvem (iCloud/OneDrive/Drive)** | ❌ não funciona no iOS (seletor de pasta + security-scoped) — UI removida |
| **Turso** (SQLite remoto) | mais simples no dado, mas sem Auth/RLS por usuário (dado de saúde exige RLS) |
| **Supabase** | ✅ escolhido: Postgres + Auth + RLS, fácil de consumir via REST |
| **Backend próprio** | futuro, se precisar de mais controle |

---

## 8. Limitações

- **Não é tempo real** — a sincronia é no botão (evolução: auto-sync ao abrir).
- **Conflito simultâneo** → vence o `updatedAt` mais recente (sem aviso). Aceitável.
- Um **usuário por sessão** por dispositivo (trocar de conta no mesmo aparelho mesclaria snapshots — evitar).
- Plano free do Supabase pode **pausar** o projeto após inatividade (~7 dias).
- O snapshot do usuário é um documento único; o tamanho cresce com o histórico (compactar tombstones no futuro).

---

## 9. Próximos passos (futuro)

- **Auto-sync ao abrir o app** ✅ (implementado — silencioso quando autenticado).
- **Realtime** do Supabase (subscription) para refletir mudanças em segundos.
- **Status/diagnóstico** na tela "Sobre".
- **Compactar tombstones**.
- **Magic link (login por link)** — ⚠️ adiado: exige captura de deep-link por plataforma; no iOS o `AvaloniaAppDelegate.OpenUrl` não é virtual (não dá para sobrescrever nesta versão do Avalonia). A infraestrutura (`SupabaseClient.SendMagicLinkAsync`/`CompleteMagicLinkAsync` + `DeepLink` bridge) está pronta; falta o hook de plataforma (ex.: intent-filter no Android, scheme no desktop). Até lá, usar **e-mail + senha**.
