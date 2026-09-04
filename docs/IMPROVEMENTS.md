# Pressio — Roadmap de melhorias (pós-MVP)

> Complementa o `PRD.md`. Interface e textos em **português (pt-BR)**.
> Legenda: 🟢 rápido · 🟡 médio · 🔴 alto (esforço/risco)

> ✔ = já feito. Itens sem o check continuam no roadmap.

---

## 0. Concluído (UX / mobile)

- ✔ **Sincronização na nuvem (Supabase)** — Auth e-mail+senha + RLS; auto-sync ao abrir.
- ✔ **Compactação de tombstones** (>30 dias) após cada sync.
- ✔ **Tela de usuários / perfil ativo no topo** — chip "USUÁRIO ATIVO" abre a tela (lista + CRUD + "Definir como ativo"); nomenclatura de UI mudada de "paciente" → "usuário".
- ✔ **Botões de ação no cabeçalho (mobile)** — rodapé oculto no mobile (teclado o cobria); Salvar no topo, Voltar cancela.
- ✔ **Fontes maiores no mobile** — `MobileFontSizeConverter` (textos ~1.8, controles ~1.2).
- ✔ **Ajustes visuais** — detalhes opcionais após Medicação; engrenagem/Configurações; correção de notificação duplicada no iOS/Android.
- ✔ **Edge-to-edge no iOS** — fundo ocupa a tela inteira (safe area tratado via `AutoSafeAreaPadding=False` + `Border` com padding do `InsetsManager.SafeAreaPadding.Top`).

---

## 1. Sincronização entre dispositivos ⭐ (prioridade)

Meta: usar o app no celular **e** no computador com os mesmos dados, **sem servidor próprio**.

Como hoje **não há infraestrutura de servidor**, a sincronização usa **Supabase** (plano gratuito) com **Auth por e-mail + senha** e **RLS** (cada usuário tem os próprios dados). O app sincroniza o snapshot do usuário logado via **Configurações → Sincronização → "Sincronizar agora"**.

Leia o design técnico completo: **[docs/SYNC.md](SYNC.md)** (modelo nuvem/Supabase + RLS, e as alternativas consideradas — pasta/iCloud foi descartada por não funcionar no iOS).

Alternativas sem servidor (candidatas, da mais simples à mais robusta):

| Opção | Prós | Contras | Esforço |
|---|---|---|---|
| **Pasta de nuvem (recomendada)** | Zero infra; os apps de nuvem já cuidam do transporte | Manual; conflito raro (last-write-wins) | 🟡 |
| **Pasta compartilhada de rede (LAN)** | Sem conta de nuvem | Só funciona na mesma rede | 🟡 |
| **Exportar/Importar `.json` (arquivo avulso)** | Simples, funciona com qualquer artefato | Totalmente manual | 🟢 |
| **WebDAV (Nextcloud/Dropbox via URL)** | Disparado por URL | Exige conta/pasta compatível | 🟡 |
| **Supabase / Turso (free tier)** | Tempo real + multi-dispositivo automático | Dependência externa, limites | 🔴 |

---

## 2. Utilidade (valor imediato)

- **Classificação por faixas** 🟢 — categorizar cada leitura (Normal, Elevada, Hipertensão estágio 1/2) conforme diretriz brasileira, com cor. Entendimento instantâneo.
- **Insights de correlação** 🟡 — "sua média sobe X mmHg nos dias com café/estresse" usando os contextos que já capturamos (estatística simples local, sem IA).
- **Média móvel 7/30 dias por horário** 🟢 — média por manhã/tarde/noite e antes/depois da medicação, mais útil que a média global.
- **Relatório "carta" para o médico** 🟢 — PDF curto com faixas de normalidade + leituras mais relevantes (reusa o export existente).
- **Lembrete pós-consulta** 🟢 — sugestão de nova rotina/aferição ao final.
- **Histórico com mais contexto no gráfico** 🟡 — pontos coloridos por faixa e marcadores de contexto (medicação, fatores).

## 3. Inovação (diferenciação)

- **Leitura por foto do monitor** 🔴 — câmera + OCR (Vision no iOS/macOS, ML Kit no Android) para preencher automaticamente. Altamente diferencial; havia sido adiado, mas é viável com as APIs nativas.
- **Apple Health / Google Fit** 🔴 — exportar as medições para as plataformas de saúde nativas. Bom alcance; exige APIs de saúde e permissões.
- **Perfis familiares comparativos** 🟡 — comparar gráficos de vários pacientes (já há suporte a múltiplos perfis).
- **Insights por IA (LLM)** 🔴 — resumo em linguagem natural; opcional (nuvem/tokenizado) ou heurísticas locais.

## 4. Fundação / robustez

- **Testes de integração do `MainViewModel`** 🟡 — cobrir agendamento de lembretes, filtros e dashboard (hoje só parser/repos têm teste).
- **Tela "Sobre" com diagnóstico** 🟢 — versão, tamanho do banco, último sync, caminho do banco.
- **Multi-idioma (pt/en)** 🔴 — pós-estabilização.

---

## 5. Prioridade sugerida

1. 🔴 **Sincronização na nuvem (Supabase)** — implementada (Auth + RLS por usuário).
2. 🟢 **Faixas de classificação** + 🟢 **média móvel por horário** — rápidas e visíveis.
3. 🟡 **Correlações** + 🟡 **testes de integração**.
4. 🔴 **Leitura por foto** (após a sincronia estabilizar).
