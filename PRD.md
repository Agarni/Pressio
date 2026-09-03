# PRD — Pressio

**Status:** Em desenvolvimento (MVP parcial)  
**Versão:** 1.2  
**Plataforma:** Windows, macOS, Linux, Android, iOS e tablets  
**Tecnologias:** Avalonia UI, .NET 10, C#, ReactiveUI e SQLite  
**Direção visual aprovada:** Índigo Premium, com cor primária personalizável

**Decisão de interação (v1.2):** formulários de pacientes, aferições e preferências serão exibidos em diálogo modal centralizado em desktop e tablets com tela física a partir de 11 polegadas. Em celulares, cada formulário abrirá como uma tela própria, com retorno explícito à tela anterior.

**Decisão visual (v1.3):** o botão primário adotará o comportamento de alto contraste inspirado no VS Code: preenchimento sólido, bordas discretas, texto branco, hover apenas mais claro e estado pressionado mais escuro. Nenhum estado poderá reduzir o contraste ou tornar o controle visualmente invisível.

## 0. Estado de implementação e roadmap

O MVP está em andamento. Abaixo, o mapa do que já existe e o cronograma de construção acordado. Itens `[x]` estão implementados; itens `[ ]` estão planejados para as próximas fases.

### Já implementado

- [x] Cadastro de múltiplos pacientes (adicionar, editar, excluir, alternar).
- [x] Campo único para pressão no formato `13/8` ou `130/80`, com conversão e validação internas.
- [x] Antes/depois da medicação e observação livre.
- [x] Data e hora preenchidas automaticamente e editáveis.
- [x] Histórico cronológico por paciente (listar, editar, excluir).
- [x] Dashboard básico (última pressão, média, contagem, gráfico de linha simples).
- [x] Exportação em CSV (sem filtros).
- [x] Tema claro/escuro e escolha de cor primária (Índigo, Azul, Verde, Roxo, Coral).
- [x] Layout responsivo: formulários em diálogo modal no desktop e tela cheia em celulares.
- [x] Persistência de aparência e cor primária (aplicadas somente ao confirmar em "Aplicar").
- [x] Confirmação de exclusão de pacientes e medições (diálogo de confirmação).
- [x] Tema escuro aplicado às superfícies, cartões e diálogos (via `DynamicResource`).
- [x] Separador de espaço no parser de pressão (além de `/` e `x`).
- [x] Fatores contextuais da aferição: seleção por chips e exibição no histórico.
- [x] Histórico com filtros (período, horário, medicação) e busca em observações, refletindo no dashboard.
- [x] Dashboard com comparação antes/depois da medicação, distribuição por horário, fatores associados e gráfico de pressão maior/menor.
- [x] Formulário de medição ampliado: frequência cardíaca, repouso, braço e posição (opcionais).
- [x] Formato de exibição configurável (`13/8` ou `130/80`), persistido nas configurações.
- [x] Relatórios: CSV e PDF (com resumo, gráfico e tabela), respeitando os filtros ativos.
- [x] Lembretes: horários recorrentes, dias da semana e ativar/desativar (aviso in-app enquanto o app está aberto).

### Roadmap

- [ ] **Fase 1 — Consistência/fundação:** falta refatorar os formulários em Views/ViewModels separadas via `ViewLocator`. (`separador de espaço no parser`, `persistir aparência/cor primária` e `confirmação de exclusão` já concluídos.)
- [x] **Fase 2 — Contexto da aferição (4.3):** entidade e coluna de fatores contextuais, seleção por chips no formulário e exibição no histórico.
- [x] **Fase 3 — Histórico e filtros (4.4):** filtro por período, horário e medicação; busca em observações; refletindo no dashboard.
- [x] **Fase 4 — Dashboard (4.5):** comparação antes/depois, distribuição por horário, fatores associados e gráfico de sistólica e diastólica (média e contagem já refletem filtros).
- [x] **Fase 5 — Formulário ampliado (4.2):** frequência cardíaca, repouso, braço/posição e formato de exibição configurável.
- [x] **Fase 6 — Relatórios (4.6):** exportação CSV e PDF com resumo e gráfico, respeitando filtros.
- [x] **Fase 7 — Lembretes (4.7):** recurso de lembretes recorrentes (horário, dias, ativo) com aviso in-app.
- [x] **Fase 8 — Qualidade e NFRs (6/etapa 10):** testes (parser e repositórios), backup/restauração, acessibilidade e correção do aviso `NU1903`.

## 1. Visão do produto

O Pressio será uma aplicação multiplataforma para registrar, acompanhar e compartilhar o histórico de pressão arterial de um ou mais usuários/pacientes. A experiência será orientada a pessoas sem conhecimento técnico de saúde: o usuário informará a medida no formato familiar `13/8` ou `130/80`, sem precisar conhecer os termos “sistólica” e “diastólica”.

A aplicação é um instrumento de registro e acompanhamento, não de diagnóstico, prescrição ou alteração de medicamentos.

## 2. Objetivos

- Tornar o lançamento de uma medição rápido e compreensível.
- Permitir o acompanhamento de vários pacientes.
- Registrar se a aferição ocorreu antes ou depois da medicação.
- Registrar circunstâncias que possam influenciar a medição.
- Oferecer histórico, dashboards e relatórios para acompanhamento.
- Funcionar de maneira consistente em celular, tablet e desktop.

## 3. Público-alvo

- Pessoas que monitoram a própria pressão.
- Familiares e cuidadores.
- Usuários que acompanham mais de um paciente.
- Profissionais de saúde que recebem relatórios exportados.

## 4. Escopo funcional

### 4.1 Pacientes

Cadastro de múltiplos perfis com:

- Nome completo e nome preferencial;
- Data de nascimento;
- Sexo, opcional;
- Altura e peso, opcionais;
- Alergias;
- Condições de saúde conhecidas;
- Medicamentos em uso;
- Médico ou contato de referência;
- Observações gerais.

O usuário poderá alternar entre pacientes e deverá haver indicação visual clara do paciente ativo.

### 4.2 Lançamento simplificado da pressão

O fluxo principal terá um único campo destacado, com exemplos visíveis:

- `13/8`
- `130/80`

Regras de interpretação:

- Separadores aceitos: `/`, espaço ou `x`, conforme decisão de UX;
- `13/8` será convertido internamente para `130/80`;
- `130/80` será mantido como `130/80`;
- O sistema armazenará os componentes separadamente como valores sistólico e diastólico;
- O formato apresentado ao usuário poderá ser escolhido nas configurações;
- Mensagens de validação utilizarão linguagem simples, por exemplo: “Informe a pressão no formato 13/8 ou 130/80”.

Dados adicionais:

- Data e hora, preenchidas automaticamente e editáveis;
- Frequência cardíaca, opcional;
- Antes ou depois da medicação;
- Repouso antes da aferição;
- Braço utilizado e posição corporal, opcionais;
- Observação livre.

O aplicativo não deverá exigir que o usuário conheça ou preencha diretamente os termos “sistólica” e “diastólica”. Esses nomes poderão aparecer apenas em detalhes técnicos, relatórios ou configurações avançadas.

### 4.3 Contexto da aferição

Opções rápidas, selecionáveis por ícones ou chips:

- Estresse ou ansiedade;
- Dor;
- Febre ou mal-estar;
- Atividade física recente;
- Café ou energético;
- Álcool;
- Tabagismo recente;
- Sono insuficiente;
- Atraso ou esquecimento da medicação;
- Alimentação diferente do habitual;
- Sintomas percebidos;
- Outro fator personalizado.

### 4.4 Histórico

- Lista cronológica por paciente;
- Exibição amigável no formato `13/8` ou `130/80`;
- Filtros por período, horário e medicação;
- Filtros por fatores contextuais;
- Busca em observações;
- Edição e exclusão com confirmação;
- Visualização dos detalhes da aferição;
- Indicadores visuais configuráveis para valores fora da faixa de referência.

### 4.5 Dashboard

O dashboard deverá mostrar:

- Última medição;
- Média no período selecionado;
- Evolução ao longo do tempo;
- Quantidade e regularidade das aferições;
- Comparação antes/depois da medicação;
- Distribuição por horário;
- Fatores associados às medições;
- Frequência cardíaca média, quando disponível.

Gráficos previstos: linha temporal, comparação de médias, calendário de registros e relação entre medida e fatores contextuais.

### 4.6 Relatórios

Exportação em PDF e CSV, com filtros por paciente, período, horário, medicação e contexto. O PDF deverá conter resumo, gráficos, tabela de aferições e observações, utilizando linguagem amigável e podendo incluir os valores técnicos em uma legenda.

### 4.7 Lembretes

- Lembretes recorrentes de aferição;
- Horários personalizados;
- Dias da semana;
- Adiamento e desativação;
- Lembretes de medicação apenas como registro/organização, sem indicação de dose ou tratamento.

## 5. Experiência e interface

- Tela inicial com paciente ativo e botão “Registrar pressão” em destaque;
- Formulário curto, com teclado numérico no celular;
- Exemplos `13/8` e `130/80` sempre próximos ao campo;
- Uso de cores, ícones e textos curtos;
- Tema claro e escuro;
- Direção visual baseada na opção C — Índigo Premium: moderna, tecnológica e orientada a dashboards;
- Permitir que o usuário escolha uma cor primária alternativa nas configurações;
- Oferecer uma paleta inicial de cores aprovadas, com pré-visualização antes de aplicar;
- Gerar automaticamente variações de contraste, estados ativos, foco e superfícies claras a partir da cor escolhida;
- Garantir contraste e legibilidade independentemente da cor selecionada;
- Layout responsivo para toque e mouse;
- Desktop e tablets a partir de 11": usar diálogos modais, com fundo atenuado, título, ação de fechar, ação de cancelar e ação principal sempre visíveis;
- Celulares: usar navegação de tela inteira para criação e edição, evitando modais estreitos ou conteúdo oculto abaixo da dobra;
- Todos os botões devem usar variantes visuais do design system: primário, secundário e destrutivo; controles padrão do framework não devem aparecer sem estilização;
- O dashboard deve refletir somente os registros persistidos do paciente e período selecionados; não deve exibir linhas ou métricas fictícias;
- Acessibilidade, contraste e fontes ajustáveis;
- Confirmação visual após o salvamento.

### 5.1 Personalização de cores

A aplicação terá uma configuração chamada **Cor de destaque** ou **Cor primária**, sem permitir que o usuário altere individualmente todas as cores do sistema.

Paleta inicial sugerida:

- Índigo — padrão aprovado;
- Azul;
- Verde;
- Turquesa;
- Roxo;
- Coral;
- Âmbar.

O sistema deverá:

- Exibir amostras nomeadas, e não apenas círculos coloridos;
- Mostrar uma prévia do dashboard e do botão de registro;
- Validar contraste automaticamente;
- Ajustar a cor do texto sobre botões quando necessário;
- Preservar cores semânticas de alerta, sucesso e erro;
- Salvar a preferência por usuário ou dispositivo, conforme a decisão de sincronização;
- Manter o Índigo como opção padrão e fallback seguro.

## 6. Requisitos não funcionais

- Funcionamento offline para lançamentos e consultas;
- Banco local SQLite;
- Backup e restauração;
- Proteção por PIN, senha ou biometria, conforme plataforma;
- Dados sensíveis ausentes de logs;
- Adequação à LGPD;
- Boa performance com milhares de registros;
- Testes em diferentes resoluções e sistemas operacionais.

## 7. Arquitetura técnica

### Cliente

- Avalonia UI;
- .NET 10;
- C#;
- ReactiveUI para estado, comandos, validação reativa, observabilidade e navegação;
- Separação entre Views, ViewModels ReactiveUI, casos de uso e serviços de plataforma;
- Projetos específicos para desktop, Android e iOS quando necessário.

### Domínio e dados

Entidades principais:

- `Patient`;
- `BloodPressureMeasurement`;
- `Medication`;
- `HealthCondition`;
- `MeasurementContext`;
- `Reminder`;
- `ReportConfiguration`.

Na entidade `BloodPressureMeasurement`, `Systolic` e `Diastolic` serão campos numéricos internos. Um serviço de entrada, por exemplo `BloodPressureParser`, converterá formatos amigáveis como `13/8` para `130/80`, validará limites técnicos e retornará mensagens compreensíveis.

Persistência atual: SQLite com `Microsoft.Data.Sqlite` puro (sem Entity Framework Core nem Dapper), com schema criado por `CREATE TABLE IF NOT EXISTS` e migrações manuais que verificam `PRAGMA table_info` antes de `ALTER TABLE`. Ao adicionar uma coluna, seguir o mesmo padrão.

## 8. MVP

- Cadastro de múltiplos pacientes;
- Registro simplificado `13/8` e `130/80`;
- Conversão e validação internas;
- Antes/depois da medicação;
- Fatores contextuais;
- Histórico e filtros;
- Dashboard básico;
- Gráficos de evolução;
- Exportação PDF e CSV;
- Lembretes locais;
- Backup manual;
- Tema claro e escuro;
- Sistema de temas com cor primária personalizável;
- Android, iOS e desktop.

Fora do MVP: integração Bluetooth, sincronização em nuvem, portal médico, inteligência artificial, prescrição e integração hospitalar.

## 9. Etapas do projeto

| Etapa | Entregas principais |
|---|---|
| 1. Descoberta | Personas, regras de negócio, requisitos e validação do formato simplificado |
| 2. UX/UI | Fluxos, protótipo, teste do lançamento `13/8`/`130/80`, identidade visual Índigo Premium, seletor de cores e responsividade |
| 3. Arquitetura | Solução Avalonia/.NET 10, ReactiveUI, SQLite, segurança e estratégia mobile |
| 4. Fundação | Projetos, navegação ReactiveUI, temas, banco, migrações e CI |
| 5. Cadastro | Pacientes, condições de saúde e medicamentos |
| 6. Medições | Parser, validação, lançamento, contexto, histórico e edição |
| 7. Dashboard | Médias, filtros, gráficos e comparação antes/depois da medicação |
| 8. Relatórios | PDF, CSV, filtros e layout para impressão |
| 9. Lembretes | Recorrência e notificações por plataforma |
| 10. Qualidade | Testes unitários, integração, usabilidade, acessibilidade e segurança |
| 11. Beta | Distribuição controlada, feedback e correções |
| 12. Publicação | Empacotamento, lojas, documentação e suporte |

Estimativa do MVP: **17 a 24 semanas**, dependendo da equipe e do nível de refinamento das plataformas mobile.

## 10. Critérios de aceite

- Um usuário leigo consegue registrar uma pressão sem conhecer os termos técnicos;
- `13/8` e `130/80` produzem o mesmo resultado interno;
- Entradas inválidas exibem orientação clara;
- É possível manter históricos independentes para vários pacientes;
- O registro de antes/depois da medicação aparece no histórico, dashboard e relatório;
- Fatores contextuais podem ser consultados posteriormente;
- Médias e gráficos respeitam filtros e período selecionados;
- O aplicativo funciona offline;
- O lançamento funciona em celular, tablet e desktop;
- O usuário consegue escolher outra cor primária e visualizar o resultado antes de aplicar;
- A cor escolhida mantém contraste, legibilidade e diferenciação dos estados de alerta;
- Não são exibidos diagnósticos ou recomendações de tratamento.

## 11. Decisões necessárias para aprovação

1. O formato padrão visual será `13/8` ou `130/80`?
2. O MVP será somente local ou incluirá sincronização em nuvem?
3. Haverá login e perfis de acesso?
4. Quais plataformas serão prioritárias no primeiro lançamento?
5. O PDF será suficiente ou será necessário exportar também para Excel?
6. O produto será validado por um profissional de saúde antes da publicação?
7. A personalização de cores será global no dispositivo ou individual por perfil de usuário?

## 12. Riscos

- Diferenças entre notificações de Android, iOS e desktop;
- Usabilidade inadequada em telas pequenas;
- Interpretação incorreta de entradas ambíguas;
- Requisitos adicionais de privacidade e LGPD;
- Necessidade de validação clínica das faixas e mensagens;
- Complexidade futura de sincronização e integração com dispositivos.
