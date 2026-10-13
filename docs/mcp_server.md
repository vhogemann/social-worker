# SocialWorker MCP Server: Guia e Referência Completa

Este documento fornece a especificação arquitetural, protocolos de transporte e o catálogo exaustivo de ferramentas e recursos do servidor **Model Context Protocol (MCP)** do `social-worker`.

---

## 1. Visão Geral e Arquitetura

O servidor MCP do `social-worker` expõe todas as capacidades operacionais da plataforma (gerenciamento de rascunhos, esteiras de automação, ingestão de fontes, publicação agendada, monitoramento de engajamento e agentes autônomos de resposta) para agentes de IA locais e remotos.

### 1.1 Especificação e Protocolos
- **Versão do Protocolo MCP**: `2024-11-05` (JSON-RPC 2.0).
- **Transporte SSE Padrão (Recomendado)**:
  - `GET /api/mcp/sse` — Estabelece stream Server-Sent Events (SSE) autenticado. Emite `event: endpoint` contendo a URL de envio de mensagens com `sessionId` (ex: `/api/mcp/message?sessionId={id}`).
  - `POST /api/mcp/message?sessionId={id}` — Envia comandos JSON-RPC do cliente para a sessão ativa.
  - `POST /api/mcp/sse?sessionId={id}` — Fallback para clientes SSE legados.
- **Transporte Stateless Direto (CLI / cURL)**:
  - `POST /api/mcp/` — Execução direta e síncrona de requisição/resposta JSON-RPC com autenticação via bearer token.
- **Multitenancy & Sessões**:
  - Sessões gerenciadas pelo `McpSessionManager` em memória via canais assíncronos (`System.Threading.Channels`).
  - O isolamento de dados do usuário é garantido pelo `ClaimsPrincipal.GetUserId()` da requisição HTTP.

### 1.2 Recursos MCP Suportados
Além de ferramentas (`tools`), o servidor implementa o protocolo de recursos (`resources`):
- `socialworker://feeds` (`application/json`): Lista completa de feeds cadastrados.
- `socialworker://workflows` (`application/json`): Lista de todos os fluxos de trabalho configurados.
- `socialworker://drafts/{draftId}` (`text/markdown`): Conteúdo segmentado e fontes de um rascunho.
- `socialworker://audits/{workflowId}` (`application/json`): Histórico recente de auditoria de comentários e respostas.

---

## 2. Catálogo Completo de Ferramentas (40 Ferramentas)

As 40 ferramentas estão organizadas em 9 domínios funcionais:

```
┌─────────────────────────────────────────────────────────────┐
│                 DOMÍNIOS DO MCP REGISTRY                    │
├──────────────────────────────┬──────────────────────────────┤
│ 1. Rascunhos & Threads (7)   │ 6. Contas Sociais (1)        │
│ 2. Fontes & Ingestão (3)     │ 7. Reply Agents (6)          │
│ 3. Feeds & Descoberta (5)    │ 8. Auditoria & Respostas (3) │
│ 4. Workflows & Curação (5)   │ 9. Posts Publicados (1)      │
│ 5. Agendamento & Fila (9)    │                              │
└──────────────────────────────┴──────────────────────────────┘
```

---

### Domínio 1: Rascunhos e Composição de Threads

#### 1. `list_drafts`
Lista rascunhos de threads com status, estágio, atribuição de workflow e filtros temporais.
- **Parâmetros**:
  - `workflowId` (*string, opcional*): UUID do workflow para filtrar.
  - `isPublished` (*boolean, opcional*): Se `true`, retorna apenas rascunhos com posts publicados.
  - `status` (*string, opcional*): Status do rascunho (`Draft`, `Scheduled`, `Published`, `Failed`).
  - `stage` (*string, opcional*): Estágio da esteira (`RoughDraft`, `Sourcing`, `Refining`, `Formatting`, `Ready`, `Sent`).
  - `since` (*string, opcional*): Timestamp ISO-8601 para filtrar criações/atualizações recentes.
  - `limit` (*integer, opcional*): Máximo de itens (padrão: 20).
- **Retorno**: Array de `DraftSummaryDto` contendo `id`, `title`, `stage`, `status`, `publishedPostsCount`, `createdAt`, `updatedAt`.

#### 2. `get_draft`
Recupera o conteúdo integral de um rascunho, seus segmentos de thread, variantes de plataforma e fontes anexadas.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
- **Retorno**: `DraftDetailsDto` com `segments` (ordem, conteúdo, anexos), `sources`, `variants` (por plataforma) e `publishedThreads`.

#### 3. `create_draft`
Cria um novo rascunho no estágio inicial (`RoughDraft`).
- **Parâmetros**:
  - `title` (*string, obrigatório*): Título do rascunho.
  - `content` (*string, opcional*): Conteúdo markdown dos segmentos. Separe múltiplos posts usando `---`.
  - `platform` (*string, opcional*): Plataforma alvo (ex: `MetaThreads`, `Bluesky`).
- **Retorno**: `DraftDto` recém-criado.

#### 4. `update_draft`
Atualiza o título ou conteúdo dos segmentos de um rascunho existente.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `title` (*string, opcional*): Novo título.
  - `content` (*string, opcional*): Novo conteúdo markdown (segmentos separados por `---`).
- **Retorno**: `DraftDto` atualizado.

#### 5. `validate_draft`
Valida o conteúdo de cada segmento contra as regras e limites rígidos da plataforma alvo.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `platform` (*string, opcional*): Plataforma alvo (padrão: `Bluesky`). Suporta `MetaThreads`.
- **Retorno**: `ValidationResultDto` contendo `isValid`, lista de `errors` (excesso de caracteres, links inválidos) e contagem de caracteres por segmento.

#### 6. `delete_draft`
Exclui permanentemente um rascunho não publicado e remove-o de filas de agendamento associadas.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
- **Retorno**: Confirmação textual de exclusão.

#### 7. `publish_thread`
Publica imediatamente um rascunho na plataforma alvo com validação do servidor.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `platform` (*string, obrigatório*): Plataforma alvo (ex: `Bluesky`, `MetaThreads`).
- **Retorno**: Detalhes dos posts criados com links e IDs remotos na rede social.

---

### Domínio 2: Fontes de Conteúdo & Ingestão

#### 8. `list_sources`
Lista todas as fontes anexadas a um rascunho específico.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
- **Retorno**: Array de `SourceDto` contendo tipo (`Url`, `Note`, `File`), título, status de processamento e resumo.

#### 9. `add_source`
Anexa uma URL, vídeo ou nota textual ao rascunho, disparando raspagem em segundo plano ou transcrição via Whisper.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `reference` (*string, obrigatório*): URL do artigo, link do YouTube ou conteúdo em texto.
  - `title` (*string, opcional*): Título descritivo da fonte.
- **Retorno**: `SourceDto` criado com status inicial de processamento.

#### 10. `fetch_source`
Recupera o texto integral extraído, metadados e sumário gerado para uma fonte anexada.
- **Parâmetros**:
  - `sourceId` (*string, obrigatório*): UUID da fonte.
- **Retorno**: `SourceContentDto` com o texto completo processado.

---

### Domínio 3: Feeds RSS, YouTube & Instagram

#### 11. `list_feeds`
Lista todos os feeds RSS, canais do YouTube e perfis inscritos.
- **Parâmetros**: Nenhum.
- **Retorno**: Array de `FeedSubscriptionDto` contendo `id`, `title`, `feedUrl`, `websiteUrl`, `includeFilters`, `excludeFilters`, `lastPolledAt`.

#### 12. `discover_feed`
Descobre a URL real do feed XML/Atom a partir de uma página web, canal do YouTube ou perfil do Instagram.
- **Parâmetros**:
  - `url` (*string, obrigatório*): URL de entrada (ex: `https://www.youtube.com/@Canal` ou `https://instagram.com/usuario`).
- **Retorno**: Objeto contendo `feedUrl`, `title` e `websiteUrl`.

#### 13. `add_feed`
Inscreve um novo feed RSS, canal do YouTube ou feed customizado.
- **Parâmetros**:
  - `title` (*string, obrigatório*): Nome de exibição.
  - `feedUrl` (*string, obrigatório*): URL direta do feed XML/Atom.
  - `websiteUrl` (*string, opcional*): Link do portal ou canal.
  - `includeFilters` (*string, opcional*): Palavras-chave obrigatórias (separadas por vírgula).
  - `excludeFilters` (*string, opcional*): Palavras-chave proibidas para descarte automático.
- **Retorno**: `FeedSubscriptionDto` cadastrado.

#### 14. `update_feed`
Atualiza título, URLs ou filtros de inclusão/exclusão de um feed existente.
- **Parâmetros**:
  - `subscriptionId` (*string, obrigatório*): UUID do feed.
  - `title` (*string, opcional*): Novo título.
  - `feedUrl` (*string, opcional*): Nova URL do feed.
  - `websiteUrl` (*string, opcional*): Nova URL do site.
  - `includeFilters` (*string, opcional*): Lista de palavras-chave permitidas.
  - `excludeFilters` (*string, opcional*): Lista de palavras-chave descartadas.
- **Retorno**: `FeedSubscriptionDto` atualizado.

#### 15. `trigger_feed_poll`
Força a sincronização imediata de um feed específico ou de todos os feeds.
- **Parâmetros**:
  - `subscriptionId` (*string, opcional*): UUID do feed (se omitido, sincroniza todos).
- **Retorno**: Resumo dos itens novos descobertos e ingeridos.

---

### Domínio 4: Workflows de Automação e Curação

#### 16. `list_workflows`
Lista todas as esteiras automatizadas de curadoria e geração de threads.
- **Parâmetros**: Nenhum.
- **Retorno**: Array de `AutomationWorkflowDto` com `id`, `title`, `instructionPrompt`, `sources` (feeds associados), `destinations` (plataformas e modos) e `scheduleId`.

#### 17. `create_workflow`
Cria uma nova esteira automatizada ligando fontes, instruções para LLM e destinos sociais.
- **Parâmetros**:
  - `title` (*string, obrigatório*): Título do workflow.
  - `instructionPrompt` (*string, opcional*): Prompt do sistema para geração de threads a partir de itens novos.
  - `feedSubscriptionIds` (*array de strings, opcional*): Lista de UUIDs de feeds associados.
  - `destinations` (*array de objetos, opcional*): Destinos de publicação:
    - `platform` (*string, obrigatório*): `MetaThreads`, `Bluesky`, etc.
    - `accountId` (*string, opcional*): UUID da conta conectada.
    - `publishingMode` (*string, opcional*): `Manual`, `Automatic` ou `Scheduled`.
    - `autoPublish` (*boolean, opcional*): Atalho para `Automatic`.
    - `scheduleId` (*string, opcional*): UUID da agenda quando `Scheduled`.
  - `scheduleId` (*string, opcional*): Agenda global do workflow.
- **Retorno**: `AutomationWorkflowDto` criado.

#### 18. `update_workflow`
Atualiza título, prompt de instruções, fontes ou destinos de um workflow existente.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
  - `title` (*string, opcional*): Novo título.
  - `instructionPrompt` (*string, opcional*): Novas instruções de geração.
  - `feedSubscriptionIds` (*array de strings, opcional*): Nova lista completa de feeds.
  - `destinations` (*array de objetos, opcional*): Nova lista completa de destinos.
  - `scheduleId` (*string, opcional*): Novo vínculo de agenda.
- **Retorno**: `AutomationWorkflowDto` atualizado.

#### 19. `delete_workflow`
Exclui um workflow e desvincula suas fontes e destinos.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
- **Retorno**: Confirmação textual de exclusão.

#### 20. `trigger_workflow`
Dispara execução imediata de um workflow (leitura de novos itens nos feeds, geração de threads via LLM e enfileiramento/publicação).
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
- **Retorno**: Quantidade de novos rascunhos criados e itens processados.

---

### Domínio 5: Agendamento & Fila de Publicação

#### 21. `list_schedules`
Lista cronogramas de publicação configurados.
- **Parâmetros**:
  - `activeOnly` (*boolean, opcional*): Se `true`, retorna apenas agendas ativas.
- **Retorno**: Array de `ScheduleDto` com timezone, contagem de regras de slot e número de posts na fila.

#### 22. `get_schedule`
Recupera detalhes de uma agenda com suas janelas recorrentes de publicação e itens na fila.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID da agenda.
- **Retorno**: `ScheduleDetailsDto` com regras (`slotRules`) e itens na fila (`queueItems`).

#### 23. `create_schedule`
Cria um novo cronograma com fuso horário IANA.
- **Parâmetros**:
  - `name` (*string, obrigatório*): Nome da agenda.
  - `timezone` (*string, opcional*): Fuso horário IANA (ex: `America/Sao_Paulo`, padrão: `UTC`).
- **Retorno**: `ScheduleDto` criado.

#### 24. `add_slot_rule`
Adiciona uma janela recorrente de postagem a um cronograma.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID da agenda.
  - `recurrenceType` (*string, obrigatório*): `Daily`, `Weekly`, `Monthly`, `Yearly` ou `CustomCron`.
  - `startTime` (*string, obrigatório*): Horário de início em formato `HH:mm:ss` (ex: `09:00:00`).
  - `endTime` (*string, opcional*): Horário de término da janela (se omitido, o slot é instantâneo).
  - `postCapacity` (*integer, opcional*): Limite de postagens por janela (padrão: 1).
  - `daysOfWeek` (*array de integers, opcional*): Dias da semana (1=Seg, 2=Ter, ..., 7=Dom).
  - `dayOfMonth` (*integer, opcional*): Dia do mês (1-31).
  - `cronExpression` (*string, opcional*): Expressão cron padrão (obrigatório se `CustomCron`).
- **Retorno**: Regra criada.

#### 25. `delete_slot_rule`
Exclui uma regra recorrente de postagem de um cronograma, ou exclui todas as regras se `slotRuleId` for omitido.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID da agenda.
  - `slotRuleId` (*string, opcional*): UUID da regra de slot específica a remover. Se omitido, remove todas as regras da agenda.
- **Retorno**: Confirmação textual da exclusão com reavaliação imediata da agenda.

#### 26. `enqueue_draft`
Enfileira um rascunho na fila de publicação de uma agenda.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID da agenda.
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `targetPosition` (*integer, opcional*): Posição específica (1-indexed). Se omitido, entra no final.
- **Retorno**: Item da fila criado com posição e horário projetado.

#### 26. `get_schedule_queue`
Lista a fila ordenada de publicações pendentes em um cronograma.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID da agenda.
- **Retorno**: Array de `ScheduleQueueItemDto` com `id`, `position`, `scheduledFor`, `status`, `draftTitle`.

#### 27. `clear_schedule_queue`
Limpa a fila de agendamento de uma agenda, com filtro opcional por workflow.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID da agenda.
  - `workflowId` (*string, opcional*): Se fornecido, remove apenas itens originados deste workflow.
- **Retorno**: Quantidade de itens removidos.

#### 28. `get_calendar_events`
Projeta as ocorrências de publicação em um intervalo de calendário.
- **Parâmetros**:
  - `start` (*string, obrigatório*): Timestamp ISO-8601 inicial.
  - `end` (*string, obrigatório*): Timestamp ISO-8601 final.
  - `scheduleId` (*string, opcional*): Filtrar por agenda específica.
- **Retorno**: Array de eventos do calendário com horários e postagens mapeadas.

---

### Domínio 6: Contas Sociais Conectadas

#### 29. `list_accounts`
Lista contas conectadas com segurança (chaves e tokens de acesso são omitidos).
- **Parâmetros**:
  - `platform` (*string, opcional*): Filtro por plataforma (`MetaThreads`, `Bluesky`, `Twitter`, `LinkedIn`).
- **Retorno**: Array de `AccountDto` contendo `id`, `platform`, `handle`, `displayName`, `isActive`.

---

### Domínio 7: Agentes Autônomos de Resposta (Reply Agents)

#### 30. `list_reply_agents`
Lista todos os agentes de resposta da biblioteca do usuário com métricas e workflows atribuídos.
- **Parâmetros**: Nenhum.
- **Retorno**: Array de `ReplyAgentDto` com `id`, `name`, `isEnabled`, `autoPublishReplies`, `assignedWorkflowsCount`, `workflows`.

#### 31. `get_reply_agent`
Obtém configuração detalhada, persona e estatísticas de um agente ou do agente associado a um workflow.
- **Parâmetros**:
  - `agentId` (*string, opcional*): UUID do agente (se fornecido).
  - `workflowId` (*string, opcional*): UUID do workflow para localizar o agente vinculado.
- **Retorno**: Objeto contendo `configured`, dados do agente (`agent`) e estatísticas de auditoria (`stats`: `totalAudited`, `repliesPublished`, `pendingApproval`).

#### 32. `create_reply_agent`
Cria um novo agente de resposta na biblioteca.
- **Parâmetros**:
  - `name` (*string, obrigatório*): Nome do agente.
  - `description` (*string, opcional*): Descrição do propósito.
  - `isEnabled` (*boolean, opcional*): Habilita o agente (padrão: true).
  - `personaPrompt` (*string, opcional*): Prompt de sistema da persona e regras de moderação.
  - `minRankingScore` (*integer, opcional*): Nota mínima de relevância (1-10) para disparar resposta (padrão: 7).
  - `maxRepliesPerCommentThread` (*integer, opcional*): Máximo de respostas no mesmo fio (padrão: 3).
  - `maxRepliesPerUser` (*integer, opcional*): Máximo de respostas ao mesmo usuário (padrão: 2).
  - `maxThreadDepth` (*integer, opcional*): Profundidade máxima na árvore de comentários (padrão: 3).
  - `maxCommentAgeHours` (*integer, opcional*): Idade máxima do comentário em horas (padrão: 24).
  - `maxPostAgeDays` (*integer, opcional*): Monitora posts publicados nos últimos N dias (padrão: 7).
  - `autoPublishReplies` (*boolean, opcional*): Se `true`, posta diretamente; se `false`, aguarda aprovação manual (padrão: true).
  - `checkIntervalMinutes` (*integer, opcional*): Intervalo do loop de monitoramento em minutos (padrão: 15).
- **Retorno**: `ReplyAgentDto` criado.

#### 33. `update_reply_agent`
Atualiza configurações e o prompt da persona de um agente de resposta existente.
- **Parâmetros**:
  - `agentId` (*string, obrigatório*): UUID do agente.
  - `name` (*string, obrigatório*): Nome do agente.
  - Demais parâmetros idênticos a `create_reply_agent` (*opcionais*).
- **Retorno**: `ReplyAgentDto` atualizado.

#### 34. `assign_reply_agent`
Vincula ou desvincula um agente de resposta a um workflow de automação.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
  - `agentId` (*string, opcional*): UUID do agente a vincular. Se omitido/nulo, desvincula o agente do workflow.
- **Retorno**: `ReplyAgentDto` com os workflows atualizados.

#### 35. `configure_reply_agent`
Configura ou cria um agente de resposta diretamente com escopo no workflow indicado.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
  - Demais parâmetros de persona e thresholds de moderação.
- **Retorno**: `ReplyAgentDto` configurado.

---

### Domínio 8: Comentários, Auditoria & Moderação

#### 36. `list_comment_audits`
Lista os comentários avaliados pelo agente de resposta com nota da LLM, decisão e respostas geradas.
- **Parâmetros**:
  - `workflowId` (*string, opcional*): Filtrar por workflow.
  - `agentId` (*string, opcional*): Filtrar por agente.
  - `decision` (*string, opcional*): `Replied`, `SkippedBelowThreshold`, `SkippedStopCriteria`, `PendingApproval`, `Failed`.
  - `limit` (*integer, opcional*): Máximo de itens (padrão: 20).
  - `offset` (*integer, opcional*): Paginação (padrão: 0).
- **Retorno**: Array de `CommentAuditDto` com texto do comentário original, pontuação, justificativa e texto da resposta.

#### 37. `trigger_reply_check`
Dispara imediatamente o ciclo de polling de comentários e ranqueamento via LLM.
- **Parâmetros**:
  - `workflowId` (*string, opcional*): UUID do workflow.
  - `agentId` (*string, opcional*): UUID do agente.
  - `postId` (*string, opcional*): UUID de um post específico para checagem imediata.
  - `runSynchronously` (*boolean, opcional*): Se `true`, aguarda a execução e retorna os comentários processados na resposta; se `false`, executa em background.
- **Retorno**: Resumo dos comentários avaliados e respostas postadas ou agendadas.

#### 38. `reply_to_comment`
Aprova uma resposta pendente da auditoria ou posta uma resposta customizada diretamente a um comentário.
- **Parâmetros**:
  - `auditId` (*string, opcional*): UUID de um registro de auditoria (`PendingApproval`) para aprovar e publicar.
  - `workflowId` (*string, condicional*): Obrigatório se `auditId` for omitido.
  - `postId` (*string, condicional*): UUID do post original se `auditId` for omitido.
  - `commentId` (*string, condicional*): ID remoto do comentário na plataforma social.
  - `authorHandle` (*string, opcional*): Handle do autor do comentário.
  - `replyText` (*string, opcional*): Texto da resposta a ser postada (se omitido e `auditId` fornecido, usa a resposta gerada pelo agente).
- **Retorno**: Dados da resposta publicada e ID remoto.

---

### Domínio 9: Posts Publicados & Métricas de Engajamento

#### 39. `list_posts`
Lista postagens publicadas com métricas de comentários, links remotos e atribuição de workflow.
- **Parâmetros**:
  - `platform` (*string, opcional*): `MetaThreads`, `Bluesky`, etc.
  - `workflowId` (*string, opcional*): UUID do workflow de origem.
  - `since` (*string, opcional*): Timestamp ISO-8601 inicial.
  - `hasReplies` (*boolean, opcional*): Se `true`, retorna apenas posts que receberam comentários.
  - `limit` (*integer, opcional*): Máximo de itens (padrão: 20, máximo: 100).
- **Retorno**: Array de `PostDto` contendo `id`, `platform`, `remotePostId`, `content`, `publishedAt`, `commentsCount`, `permalink`, `workflowId`.

---

## 3. Diretrizes Operacionais e Limites de Plataforma

### 3.1 Meta Threads
- **Limite Fatal**: 500 caracteres por post.
- **Faixa Segura Recomendada**: 350 a 430 caracteres (permite cards e links nativos sem exceder o buffer).
- **Formatação**: Não suporta sintaxe markdown pesada (`**` ou `#`). Enviar texto puro com quebras de linha limpas.
- **Links Nativos**: Enviar a URL crua (`https://...`) solta no final do texto para geração de rich cards nativos.

### 3.2 Bluesky
- **Limite Fatal**: 300 caracteres por post.
- **Rich Text Facets**: Menções (`@handle`) e links são convertidos em facetas de bytes UTF-8 no momento do envio via protocolo AT.

### 3.3 Tratamento de Erros no Servidor MCP
As respostas de erro seguem o padrão JSON-RPC:
- `-32602` (*Invalid Params*): Argumento ausente ou formato inválido.
- `-32601` (*Method Not Found*): Ferramenta não registrada.
- `-32000` (*Server Error*): Falha de banco de dados ou erro de execução capturado e logado.
