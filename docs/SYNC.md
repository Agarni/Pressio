# Pressio — Design de Sincronização (sem servidor)

## 1. Objetivo e premissa

Permitir usar o Pressio em mais de um dispositivo (ex.: celular e computador) com os mesmos dados.

**PREMISSA:** não há servidor próprio. Portanto, a abordagem é **sincronização por arquivo** numa **pasta de nuvem** escolhida pelo usuário — OneDrive, Google Drive ou iCloud montado como pasta local. O transporte (upload/download/entre dispositivos) é feito **pelo cliente da nuvem**, não pelo app. O app apenas lê/grava o arquivo na pasta.

**Modo:** manual (botão "Sincronizar agora"). Sem backend, sem conta própria, sem tempo real (o usuário aciona quando quer).

> Para os arquivos de nuvem funcionarem, o cliente (OneDrive/Google Drive/iCloud) precisa estar instalado e sincronizando aquela pasta em cada dispositivo.

---

## 2. Fluxo do usuário

1. Em **Configurações → Sincronização**, o usuário escolhe uma **pasta compartilhada** (com o seletor de pasta nativo de cada plataforma).
2. Toca em **"Sincronizar agora"**.
3. O app exporta o estado local, lê o arquivo remoto (`pressio-sync.json` na pasta), **mescla** e:
   - grava o **resultado mesclado** de volta no arquivo na nuvem;
   - **importa** as mudanças para o banco local.
4. Mostra um resumo (ex.: `3 medições novas, 1 atualizada, 1 removida`).

Cada dispositivo aponta o **seu** caminho para a **mesma** pasta/arquivo. A primeira execução faz um `full sync`; as seguintes fazem merge incremental.

---

## 3. Arquivo de sincronia — `pressio-sync.json`

```jsonc
{
  "formatVersion": 1,          // versão do schema p/ migração
  "deviceId": "<guid desta máquina>",
  "exportedAt": "2026-09-03T12:00:00Z",
  "patients": [
    {
      "syncId": "<guid>",      // chave estável entre dispositivos
      "name": "João",
      "bornAt": "1965-05-01",  // opcional
      "updatedAt": "2026-09-03T10:00:00Z",
      "deleted": false,        // tombstone (marca exclusão)
      "deviceId": "<guid origem da última escrita>"
    }
  ],
  "measurements": [
    {
      "syncId": "<guid>",
      "patientSyncId": "<guid do paciente>",
      "systolic": 130,
      "diastolic": 80,
      "heartRate": 72,
      "atRest": true,
      "arm": "Right",          // enum serializado
      "position": "Seated",
      "context": 19,           // [Flags] bitmask
      "medication": 1,         // MedicationTiming enum
      "measuredAt": "2026-09-03T08:30:00Z",
      "notes": "...",
      "updatedAt": "2026-09-03T08:35:00Z",
      "deleted": false,
      "deviceId": "<guid>"
    }
  ],
  "reminders": [
    {
      "syncId": "<guid>",
      "time": "08:00:00",      // HH:mm:ss
      "days": 127,             // ReminderDays bitmask
      "enabled": true,
      "note": "...",
      "updatedAt": "2026-09-03T09:00:00Z",
      "deleted": false,
      "deviceId": "<guid>"
    }
  ],
  "settings": {
    "Theme":    { "value": "Escuro",  "updatedAt": "..." },
    "PrimaryColor": { "value": "Índigo", "updatedAt": "..." }
  }
}
```

### Por que `syncId` (Guid) e não o id inteiro do SQLite?
IDs inteiros autoincrementais são **locais** e **colidem** entre dispositivos (o `id=1` do celular não é o `id=1` do PC). A sincronia exige uma **chave globalmente única**: um `Guid`. O `id` inteiro é apenas a PK interna do banco local; o `syncId` é a identidade usada para mesclar entre dispositivos. Relações (medição→paciente) passam a usar `syncId`.

### Tombstones
Exclusões são representadas com `deleted: true` (não remoção do arquivo), para que a exclusão "viaje" para o outro dispositivo. Registros `deleted: true` mais antigos podem ser podados num futuro processo de compactação.

---

## 4. Estratégia de mesclagem (merge)

**Última escrita vence por registro** (last-write-wins por entidade), usando `updatedAt`.

```
para cada entidade em { pacientes, medições, lembretes, configurações }:
    local    por syncId
    remota   por syncId
    resultado = união das chaves
      - se só em um lado          -> inclui
      - se nos dois               -> vence o de `updatedAt` mais recente
                                   -> `deleted:true` vence (remove na prática)
```

- **Granularidade:** por registro (não "arquivo ganha"), o que evita apagar dados de um dispositivo por causa de uma escrita no outro.
- **Conflito real** (edição simultânea do mesmo registro): vence o mais recente por `updatedAt`. Para um app de saúde pessoal, é aceitável.
- **`deviceId`:** usado apenas para diagnóstico/auditoria; **não** é a base da resolução (evita "device X sempre ganha").

### Cuidado com relógios
`updatedAt` é UTC. Se o relógio de um dispositivo estiver adiantado, ele tende a "vencer" sempre. Mitigação simples e suficiente para o MVP: não exige relógio de alta precisão; já o `exportedAt` serve para estatística. (Uma evolução futura seria um *logical/vector clock*, dispensada por ora.)

---

## 5. Migração do modelo de dados (necessária)

Para suportar `syncId`, cada tabela precisa de um campo novo (string guid), seguindo o **mesmo padrão de migração** já usado (`PRAGMA table_info` → `ALTER TABLE ADD COLUMN`):

| Tabela | Coluna nova |
|---|---|
| `Patients` | `SyncId TEXT` |
| `Measurements` | `SyncId TEXT`, `PatientSyncId TEXT` |
| `Reminders` | `SyncId TEXT` |

Na migração, backfill: `UPDATE ... SET SyncId = lower(hex(randomblob(16)))` ou gerar no lado .NET para registros existentes.

> Os repositórios ganham overloads/métodos de leitura/escrita em lote (`SyncSnapshot` / `ApplySyncSnapshot`) e a escrita passa a manter `SyncId`/`PatientSyncId` atualizados.

---

## 6. Núcleo de sincronia (componentes)

- **`SyncService`** (no projeto `Pressio`): lê o snapshot local dos repositórios, mescla com o arquivo remoto e aplica o resultado.
- **`SyncStore`** (arquivo): serializa/desserializa `pressio-sync.json` (System.Text.Json), tolerante a versões (`formatVersion`).
- **Snapshot local:** DTOs para exportar as entidades com `syncId`/`updatedAt`/`deleted`.
- **Repositórios** (`MeasurementRepository`, `PatientRepository`, `ReminderRepository`, `SettingsRepository`): suporte a leitura completa p/ snapshot e aplicação em lote.
- **`MainViewModel`**: novos comandos/bindings — escolher pasta, "Sincronizar agora", e status (último sync, resumo).

O banco local continua 100% funcional offline; a UI não depende de rede.

---

## 7. Seleção de pasta por plataforma

- Usar o **seletor de pasta nativo** via Avalonia `StorageProvider.TryGetFolderPickerAsync` (`FolderPickerOpenOptions`).
  - **Desktop (Win/macOS/Linux):** escolhe uma pasta local — a pasta do OneDrive/Google Drive/iCloud **já montada** no sistema.
  - **Android:** o seletor de documentos do sistema permite escolher uma pasta de nuvem (Drive etc.) exposta pelo Storage Access Framework. *Ponto de atenção: em algumas versões/fornecedores escolher "pasta" é limitado; mitigação = permitir escolher a pasta de um app específico ou usar a pasta local com a nuvem sincronizada.*
- Persistir o caminho escolhido (e se possível, o `content://` URI no Android) nas configurações (`LastSyncDirectory`), como já é feito com `LastExportDirectory`.

---

## 8. Limitações documentadas (transparência)

- **Não é tempo real** — a sincronia acontece quando o usuário toca no botão (ou, futuramente, ao abrir o app).
- **Conflito simultâneo** de um mesmo registro → vence o `updatedAt` mais recente (sem aviso). Aceitável para uso pessoal.
- **Depende do cliente de nuvem** estar ativo e sincronizando a pasta.
- **Volume:** o arquivo cresce com o histórico (tombstones inclusos). Para volumes grandes, considerar compactação/limpeza periódica de tombstones ou particionar por ano. O MVP importa tudo.

---

## 9. Plano de implementação (fases)

- **Fase S1 — Fundação:** migração `SyncId`/`PatientSyncId` + backfill; DTOs de snapshot; serialização de `pressio-sync.json` (com testes).
- **Fase S2 — Export/Import local:** exportar o snapshot para um `.json` e importá-lo (merge) — já dá para "levar" dados entre máquinas manualmente.
- **Fase S3 — Pasta de nuvem:** seletor de pasta + "Sincronizar agora" + resumo; persistir `LastSyncDirectory`.
- **Fase S4 — Refinamentos:** auto-sync ao abrir o app (opcional); compactar tombstones; status/diagnóstico na tela "Sobre".

> **Fases S1–S2 já entregam valor real** (transferência por arquivo) sem depender de cliente de nuvem para testar.

---

## 10. Caminho futuro (quando houver servidor)

Evoluir a mesma semântica para um backend REST (`GET/PUT /measurements?updatedAfter=...`), mantendo `syncId`/`updatedAt` como chave de sincronia e *delta sync*. Assim o modo arquivo vira apenas um "transport" alternativo, sem redesenhar o modelo.
