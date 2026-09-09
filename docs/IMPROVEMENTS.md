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
- ✔ **Classificação por faixas** — cada leitura ganha um chip colorido (Ótima/Normal/Elevada/Hipertensão 1–3, 7ª Diretriz SBC 2020), na última pressão e na coluna "Classificação" do CSV.
- ✔ **Médias por horário** — média de pressão por Madrugada/Manhã/Tarde/Noite no dashboard, respeitando o filtro de período (Todo/Hoje/7/30 dias).
- ✔ **Carta ao médico** — PDF curto e limpo: resumo clínico (média, última, antes/depois, por horário), distribuição por faixa, legenda das faixas (SBC) e as leituras mais relevantes.
- ✔ **PDF multiplataforma (PDFsharp)** — relatório e carta gerados por lib 100% gerenciada (funciona em desktop/iOS/Android); gráfico com eixos + linha de referência 140/90, "Página X de Y", coluna FC e estatísticas (máx/mín, % ≥140/90, FC média). O SkiaSharp PDF crashava nativo no Android, então foi migrado.
- ✔ **Export gravando em todas as plataformas** — `StorageWriter`: Android via `ContentResolver` (SAF), iOS/desktop via `OpenWriteAsync`.
- ✔ **Folha de opções do relatório (mobile)** — botão único "Mais opções de relatório" abre uma sheet (estilo diálogo) com período + Carta/PDF/CSV/Saúde e "X" para fechar.
- ✔ **Gestos no mobile** — swipe da borda esquerda → voltar (dispensa o botão) e toque na barra de status → rolar ao topo.
- ✔ **Apple Health (iOS)** — exportar as medições para o app Saúde (HealthKit), sem conta de terceiros.
- ✔ **Android "Pressio"** — renomeado (não "Pressio.Android") e splash com o ícone do app (não o logo Avalonia).
- ✔ **Correlações com defasagem temporal + significância** — compara a pressão nas horas seguintes ao fator vs. sem o fator recente; mín. de amostras e selo "Tendência".
- ✔ **Testes de integração (lógica)** — `MeasurementFilter` (filtros) e `ReminderDueCalculator` (lembretes devidos) extraídos e testados; dashboard já coberto.
- ✔ **Tela "Sobre" com diagnóstico** — versão, caminho/tamanho do banco e último sync.
- ✔ **Ações de registro no próprio card (mobile)** — Editar/Excluir aparecem **somente no card selecionado** (via pseudo-classe `:selected`), sem cortar texto; lápis/lixeira do topo removidos.
- ✔ **Padrão de nova aferição** — Antes da medicação, Sentado, braço Esquerdo.
- ✔ **Notificações Android corretas** — permissão `POST_NOTIFICATIONS` em runtime, alarme **exato** (`setExactAndAllowWhileIdle`), `contentIntent` para abrir o app ao tocar, e o `SaveReminder` só agenda se habilitado.
- ✔ **Sync ao fechar/pausar** — desktop aguarda antes de sair; Android (`OnStop`) e iOS (`DidEnterBackground`) sincronizam ao ir para segundo plano.

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

- ✔ **Insights de correlação** — compara a pressão média nas horas seguintes a cada fator (café, estresse etc.) com as sem o fator recente; mínimo de amostras por grupo e selo "Tendência" quando a amostra é pequena.
- **Lembrete pós-consulta** 🟢 — sugestão de nova rotina/aferição ao final.
- ✔ **Histórico com mais contexto no gráfico** — pontos coloridos por faixa e seletor de período (Hoje/7/15/30 dias) no gráfico.

## 3. Inovação (diferenciação)

- **Leitura por foto do monitor** 🔴 — câmera + OCR para preencher automaticamente. **Protótipo no iOS** (câmera + Vision + decodificador 7-segmentos), mas a leitura automática não ficou confiável (display de 7 segmentos varia entre aparelhos) — **pausado**; botão oculto no form. Reavaliar com Tesseract/segmentação dedicada.
- ✔ **Apple Health (iOS)** — exportar para o app Saúde via HealthKit (sem conta). *Google Fit/Health Connect (Android): **deixado de lado** — a lib `connect-client` usa corrotinas e a escrita crasha em device; scaffold pronto (stub).*
- **Perfis familiares comparativos** 🟡 — comparar gráficos de vários pacientes. **Em standby** (pouco usado; usuário costuma registrar só o próprio).
- **Insights por IA (LLM)** 🔴 — resumo em linguagem natural; opcional (nuvem/tokenizado) ou heurísticas locais.

## 4. Fundação / robustez

- ✔ **Testes de integração (lógica)** — `MeasurementFilter` (período/medicação/horário/busca), `ReminderDueCalculator` (lembrete devido) e `DashboardCalculator` isolados e testáveis (filtros/lembretes/dashboard).
- ✔ **Tela "Sobre" com diagnóstico** — versão, caminho/tamanho do banco, último sync.
- **Multi-idioma (pt/en)** 🔴 — pós-estabilização.

---

## 5. Prioridade sugerida

1. 🔴 **Sincronização na nuvem (Supabase)** — implementada (Auth + RLS por usuário).
2. 🟢 **Faixas de classificação** + 🟢 **média móvel por horário** — rápidas e visíveis.
3. 🟡 **Correlações com defasagem temporal** + 🟡 **testes de integração** — implementados.
4. 🔴 **PDF multiplataforma (PDFsharp)** + **Apple Health (iOS)** — implementados.
