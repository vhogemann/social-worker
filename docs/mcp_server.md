# SocialWorker MCP Server: Guia e Referência Completa

Este documento fornece a especificação arquitetural, protocolos de transporte e o catálogo exaustivo de ferramentas e recursos do servidor **Model Context Protocol (MCP)** do `social-worker`.

---

## 1. Visão Geral e Arquitetura

O servidor MCP do `social-worker` expõe todas as capacidades operacionais da plataforma (gerenciamento de rascunhos, esteiras de automação, ingestão de fontes, publicação agendada, monitoramento de engajamento, geração e processamento de mídia visual e agentes autônomos de resposta) para agentes de IA locais e remotos.

### 1.1 Especificação e Protocolos
- **Versão do Protocolo MCP**: `2024-11-05` (JSON-RPC 2.0).
- **Transporte SSE Padrão (Recomendado)**:
  - `GET /api/mcp/sse` (ou `/mcp/sse`) — Estabelece stream Server-Sent Events (SSE) autenticado. Emite `event: endpoint` contendo a URL de envio de mensagens com `sessionId` (ex: `/api/mcp/message?sessionId={id}`).
  - `POST /api/mcp/message?sessionId={id}` — Envia comandos JSON-RPC do cliente para a sessão ativa.
  - `POST /api/mcp/sse?sessionId={id}` — Fallback para clientes SSE legados.
- **Transporte Stateless Direto (Streamable HTTP / CLI / cURL)**:
  - `POST /api/mcp` ou `POST /mcp` — Execução direta e síncrona de requisição/resposta JSON-RPC com autenticação via bearer token.
- **Autenticação Padrão MCP (Compatível com Claude / Anthropic)**:
  - **Descoberta RFC 9728**: Requisições não autenticadas recebem `401 Unauthorized` com cabeçalho `WWW-Authenticate: Bearer realm="mcp", resource_metadata="{baseUrl}/.well-known/oauth-protected-resource"`.
  - **Protected Resource Metadata**: Endpoint `GET /.well-known/oauth-protected-resource` informa o servidor de autorização e os escopos suportados (`mcp:tools`, `mcp:read`, `mcp:write`).
  - **Authorization Server Metadata (RFC 8414)**: Endpoint `GET /.well-known/oauth-authorization-server` (e alias `/.well-known/openid-configuration`).
  - **Dynamic Client Registration (RFC 7591)**: Endpoint `POST /oauth/register` registra clientes dinâmicos (Claude Desktop / Claude Code).
  - **Fluxo OAuth 2.1 com PKCE (S256)**:
    - `GET /oauth/authorize` — Interface visual com reautenticação obrigatória (usuário e senha, sem confiar em sessões persistidas do navegador).
    - `POST /oauth/token` — Troca de código de autorização e verifier PKCE por token de acesso JWT (1 hora) e refresh token persistente (30 dias).
  - **Compatibilidade Retroativa**: Suporte contínuo a API keys estáticas (`sw_live_...`, `X-API-Key`, e `?api_key=...`).
- **Multitenancy & Sessões**:
  - Sessões gerenciadas pelo `McpSessionManager` em memória via canais assíncronos (`System.Threading.Channels`).
  - O isolamento de dados do usuário é garantido pelo `ClaimsPrincipal.GetUserId()` da requisição HTTP.

### 1.2 Recursos MCP Suportados
Além de ferramentas (`tools`), o servidor implementa o protocolo de recursos (`resources`):
- `socialworker://feeds` (`application/json`): Lista completa de feeds cadastrados.
- `socialworker://workflows` (`application/json`): Lista de todos os fluxos de trabalho configurados.
- `socialworker://drafts/{draftId}` (`text/markdown`): Conteúdo segmentado, fontes e mídia anexada de um rascunho.
- `socialworker://audits/{workflowId}` (`application/json`): Histórico recente de auditoria de comentários e respostas.

---

## 2. Catálogo Completo de Ferramentas (66 Ferramentas)

As 66 ferramentas estão organizadas em 9 domínios funcionais:

```
┌─────────────────────────────────────────────────────────────┐
│                 DOMÍNIOS DO MCP REGISTRY                    │
├──────────────────────────────┬──────────────────────────────┤
│ 1. Rascunhos & Threads (10)  │ 6. Agendamento & Fila (16)   │
│ 2. Mídia & Visual (8)        │ 7. Contas Sociais (1)        │
│ 3. Fontes & Pesquisa (5)     │ 8. Reply Agents (8)          │
│ 4. Feeds & Descoberta (6)    │ 9. Auditoria & Posts (6)     │
│ 5. Workflows & Curação (6)   │                              │
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

#### 2. `get_draft`
Obtém os detalhes completos de um rascunho, seus segmentos, mídia anexada e threads adaptadas por plataforma.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.

#### 3. `create_draft`
Cria um novo rascunho de thread com validação inicial e retorna payload estruturado JSON.
- **Parâmetros**:
  - `title` (*string, obrigatório*): Título do rascunho.
  - `content` (*string, opcional*): Texto inicial da thread.
  - `platform` (*string, opcional*): Plataforma-alvo (`Bluesky`, `Twitter`, `MetaThreads`, `LinkedIn`).
  - `accountId` (*string, opcional*): UUID da conta social a associar.

#### 4. `update_draft`
Atualiza título ou conteúdo de um rascunho existente.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `title` (*string, opcional*): Novo título.
  - `content` (*string, opcional*): Novo texto da thread.

#### 5. `delete_draft`
Exclui permanentemente um rascunho.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.

#### 6. `discard_draft`
Descarta um rascunho arquivando-o com uma justificativa de descarte.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `reason` (*string, opcional*): Justificativa do descarte.

#### 7. `validate_draft`
Executa validação completa de limites de caracteres, imagens e formatação para a plataforma informada (ou a plataforma padrão do rascunho).
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `platform` (*string, opcional*): Plataforma-alvo (Bluesky, Twitter, MetaThreads, LinkedIn).
  - `accountId` (*string, opcional*): UUID da conta para validação de limites de nível de conta.

#### 8. `publish_thread`
Publica o rascunho imediatamente ou executa simulação segura (`dryRun: true`), com suporte a multi-plataformas e verificação de conta explícita.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `platform` (*string, opcional*): Plataforma única a publicar.
  - `platforms` (*array de strings, opcional*): Múltiplas plataformas para publicação simultânea.
  - `accountId` (*string, opcional*): UUID da conta para desambiguação de publicação.
  - `dryRun` (*boolean, opcional*): Se `true`, valida e retorna o relatório sem postar na rede social.
  - `confirm` (*boolean, opcional*): Confirmação explícita de publicação irreversível.

#### 9. `generate_variants`
Gera variações e adaptações de uma thread para plataformas sociais suportadas (Bluesky, Twitter, Threads, LinkedIn).
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho canônico.
  - `platforms` (*array de strings, opcional*): Lista de plataformas para gerar adaptações.

#### 10. `set_bluesky_reply_target`
Configura um rascunho como resposta a um post existente do Bluesky, resolvendo automaticamente os metadados do parent e root.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `replyParentUrl` (*string, obrigatório*): URL completa do post do Bluesky.

---

### Domínio 2: Mídia e Ativos Visuais

#### 11. `import_media_from_url`
Baixa uma imagem de uma URL externa, processa, otimiza e anexa ao rascunho.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `url` (*string, obrigatório*): URL pública da imagem.
  - `altText` (*string, opcional*): Texto alternativo de acessibilidade.

#### 12. `upload_media`
Faz upload direto de imagem ou arquivo de mídia codificado em base64 e anexa ao rascunho.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `fileName` (*string, obrigatório*): Nome do arquivo com extensão.
  - `base64Data` (*string, obrigatório*): Dados do arquivo em base64.
  - `mimeType` (*string, opcional*): Tipo MIME (padrão: `image/png`).
  - `altText` (*string, opcional*): Texto descritivo de acessibilidade.

#### 13. `list_templates`
Lista os templates visuais cadastrados na biblioteca do usuário com nomes de slots e dimensões.
- **Parâmetros**:
  - `search` (*string, opcional*): Termo para busca de templates.

#### 14. `render_template`
Renderiza um template gráfico substituindo os slots de texto/métricas e anexa a imagem resultante ao rascunho.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `templateId` (*string, obrigatório*): UUID ou nome do template.
  - `slots` (*object, opcional*): Dicionário de valores para preenchimento dos slots.
  - `altText` (*string, opcional*): Texto alternativo para a imagem gerada.

#### 15. `render_code_blocks`
Converte blocos de código markdown presentes no rascunho em imagens estilizadas de sintaxe destacada.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `theme` (*string, opcional*): Tema de cores do destaque de código.

#### 16. `search_images`
Busca imagens na web via SearXNG para uso como referências ou anexos visuais.
- **Parâmetros**:
  - `query` (*string, obrigatório*): Termo de busca de imagem.
  - `limit` (*integer, opcional*): Quantidade máxima de resultados (padrão: 5).

#### 17. `update_media`
Atualiza o texto alternativo (`altText`) de um ativo de mídia existente.
- **Parâmetros**:
  - `mediaId` (*string, obrigatório*): UUID do ativo de mídia.
  - `altText` (*string, obrigatório*): Novo texto alternativo.

#### 18. `delete_media`
Exclui um ativo de mídia anexado por ID.
- **Parâmetros**:
  - `mediaId` (*string, obrigatório*): UUID do ativo de mídia.

---

### Domínio 3: Fontes, Ingestão e Pesquisa

#### 19. `list_sources`
Lista fontes associadas a um rascunho ou cadastradas na biblioteca.
- **Parâmetros**:
  - `draftId` (*string, opcional*): UUID do rascunho.

#### 20. `add_source`
Adiciona uma URL, nota ou arquivo como fonte de conteúdo.
- **Parâmetros**:
  - `url` (*string, obrigatório*): URL da fonte a ser cadastrada.
  - `draftId` (*string, opcional*): UUID do rascunho para vincular diretamente.

#### 21. `fetch_source`
Executa scraping e extração de texto limpo de uma URL de fonte externa.
- **Parâmetros**:
  - `sourceId` (*string, obrigatório*): UUID da fonte.

#### 22. `transcribe_video`
Extrai o áudio e transcreve o texto falado de um vídeo (ex: YouTube) através do serviço de transcrição.
- **Parâmetros**:
  - `url` (*string, obrigatório*): URL do vídeo.

#### 23. `web_search`
Executa pesquisa web livre para enriquecimento de rascunhos e busca de fontes.
- **Parâmetros**:
  - `query` (*string, obrigatório*): Consulta de busca.

---

### Domínio 4: Feeds e Descoberta

#### 24. `list_feeds`
Lista todas as assinaturas de feed ativas.
- **Parâmetros**: nenhum.

#### 25. `discover_feed`
Identifica automaticamente URLs de feeds RSS/Atom a partir de uma página web.
- **Parâmetros**:
  - `url` (*string, obrigatório*): URL do site para descoberta.

#### 26. `add_feed`
Cadastra um novo feed RSS/Atom para monitoramento contínuo.
- **Parâmetros**:
  - `feedUrl` (*string, obrigatório*): URL do feed.
  - `title` (*string, obrigatório*): Nome do feed.
  - `websiteUrl` (*string, opcional*): URL do site principal.
  - `includeFilters` (*string, opcional*): Termos obrigatórios separados por vírgula.
  - `excludeFilters` (*string, opcional*): Termos a ignorar separados por vírgula.

#### 27. `update_feed`
Atualiza filtros ou títulos de uma assinatura de feed.
- **Parâmetros**:
  - `feedId` (*string, obrigatório*): UUID do feed.
  - `title` (*string, opcional*): Novo título.
  - `includeFilters` (*string, opcional*): Novos filtros de inclusão.
  - `excludeFilters` (*string, opcional*): Novos filtros de exclusão.

#### 28. `delete_feed`
Remove uma assinatura de feed pelo ID.
- **Parâmetros**:
  - `feedId` (*string, obrigatório*): UUID do feed.

#### 29. `trigger_feed_poll`
Dispara a busca imediata de novos artigos em um feed ou em todos os feeds.
- **Parâmetros**:
  - `feedId` (*string, opcional*): UUID do feed específico ou nulo para todos.

---

### Domínio 5: Workflows e Automação

#### 30. `list_workflows`
Lista todos os pipelines de automação com seus feeds vinculados, prompt de instrução e agendamento.
- **Parâmetros**: nenhum.

#### 31. `get_workflow`
Obtém detalhes completos de configuração, destinos e status de um workflow pelo ID.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.

#### 32. `create_workflow`
Cria um pipeline de curadoria e geração automática de threads a partir de feeds.
- **Parâmetros**:
  - `title` (*string, obrigatório*): Nome do fluxo.
  - `instructionPrompt` (*string, opcional*): Prompt com diretrizes editoriais.
  - `scheduleId` (*string, opcional*): UUID do agendamento para enfileiramento automático.
  - `minRelevanceScore` (*integer, opcional*): Nota mínima (0-10) da LLM para criar rascunho.
  - `feedSubscriptionIds` (*array de strings, opcional*): Feeds a monitorar.
  - `destinations` (*array de objetos, opcional*): Plataformas de destino com `platform`, `publishingMode`, `scheduleId`, `accountId`.

#### 33. `update_workflow`
Atualiza o título, diretrizes editoriais, destinos, agendamento ou status (`Active`, `Paused`, `Archived`) de um workflow.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
  - `title` (*string, opcional*): Novo título.
  - `instructionPrompt` (*string, opcional*): Novas instruções editoriais.
  - `status` (*string, opcional*): Novo status (`Active`, `Paused`, `Archived`).
  - `scheduleId` (*string, opcional*): Novo agendamento.
  - `destinations` (*array de objetos, opcional*): Novos destinos.

#### 34. `delete_workflow`
Exclui um pipeline de automação pelo ID.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.

#### 35. `trigger_workflow`
Executa o ciclo de avaliação de artigos e geração de rascunhos para um workflow.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.

---

### Domínio 6: Agendamento e Gestão de Fila

#### 36. `list_schedules`
Lista todos os cronogramas de publicação configurados com timezone e regras de slots.
- **Parâmetros**: nenhum.

#### 37. `get_schedule`
Retorna as regras e slots semanais de um agendamento específico.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.

#### 38. `create_schedule`
Cria um novo cronograma de postagem semanal.
- **Parâmetros**:
  - `name` (*string, obrigatório*): Nome do cronograma.
  - `timezone` (*string, opcional*): Fuso horário IANA (padrão: `UTC`).

#### 39. `update_schedule`
Atualiza nome, fuso horário ou estado ativo de um cronograma existente.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.
  - `name` (*string, opcional*): Novo nome.
  - `timezone` (*string, opcional*): Novo fuso horário.
  - `isActive` (*boolean, opcional*): Estado ativo.

#### 40. `delete_schedule`
Exclui um cronograma e suas regras de slot associadas.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.

#### 41. `add_slot_rule`
Adiciona uma regra de janela de horário recorrente (ex: Segundas às 14:00).
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.
  - `dayOfWeek` (*integer, obrigatório*): 0 (Domingo) a 6 (Sábado).
  - `timeOfDay` (*string, obrigatório*): Horário no formato `HH:mm`.
  - `allowedPlatforms` (*array de strings, opcional*): Plataformas autorizadas para o slot.

#### 42. `delete_slot_rule`
Remove uma regra de horário do cronograma.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.
  - `slotRuleId` (*string, obrigatório*): UUID da regra.

#### 43. `schedule_post`
Agenda diretamente um rascunho para publicação em uma data/hora UTC futura sem requerer regras de slot semanais prévias.
- **Parâmetros**:
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `publishAt` (*string, obrigatório*): Data e hora no formato ISO-8601 (ex: `2026-10-20T14:00:00Z`).
  - `scheduleId` (*string, opcional*): UUID do cronograma.
  - `platform` (*string, opcional*): Plataforma social alvo.
  - `accountId` (*string, opcional*): UUID da conta para publicação.

#### 44. `enqueue_draft`
Enfileira um rascunho no cronograma para publicação no próximo slot livre ou com data-alvo `publishAt`.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.
  - `draftId` (*string, obrigatório*): UUID do rascunho.
  - `platformThreadId` (*string, opcional*): UUID da thread específica de plataforma.
  - `accountId` (*string, opcional*): UUID da conta social.
  - `publishAt` (*string, opcional*): Timestamp ISO-8601 alvo opcional.

#### 45. `get_schedule_queue`
Lista os itens atualmente aguardando envio na fila de publicação.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.

#### 46. `remove_queue_item`
Remove um item específico da fila de publicação.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.
  - `queueItemId` (*string, obrigatório*): UUID do item na fila.

#### 47. `reorder_queue`
Reordena todos os itens pendentes na fila de publicação através de uma lista ordenada de IDs.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.
  - `itemIds` (*array de strings, obrigatório*): Lista completa de UUIDs de itens na nova ordem.

#### 48. `pause_queue_item`
Pausa ou retoma o envio de um item específico na fila de publicação.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.
  - `queueItemId` (*string, obrigatório*): UUID do item na fila.
  - `isPaused` (*boolean, opcional*): `true` para pausar, `false` para retomar (padrão: `true`).

#### 49. `retry_queue_item`
Reinicia e coloca novamente em estado pendente um item que falhou ou expirou.
- **Parâmetros**:
  - `queueItemId` (*string, obrigatório*): UUID do item com falha.

#### 50. `clear_schedule_queue`
Limpa todos os itens pendentes da fila de publicação.
- **Parâmetros**:
  - `scheduleId` (*string, obrigatório*): UUID do cronograma.

#### 51. `get_calendar_events`
Projeta os slots e publicações previstas em um intervalo de datas para visualização em calendário.
- **Parâmetros**:
  - `scheduleId` (*string, opcional*): Filtrar por cronograma.
  - `startUtc` (*string, obrigatório*): Início do período (ISO-8601).
  - `endUtc` (*string, obrigatório*): Fim do período (ISO-8601).

---

### Domínio 7: Contas Sociais

#### 52. `list_accounts`
Lista contas conectadas e credenciadas para publicação com identificação de plataforma, handle e ID.
- **Parâmetros**:
  - `platform` (*string, opcional*): Filtrar por plataforma social.

---

### Domínio 8: Reply Agents e Moderação Autônoma

#### 53. `list_reply_agents`
Lista todos os agentes autônomos de resposta da biblioteca do usuário.
- **Parâmetros**: nenhum.

#### 54. `get_reply_agent`
Obtém a configuração, persona e métricas de um agente de resposta pelo ID do agente ou do workflow vinculado.
- **Parâmetros**:
  - `agentId` (*string, opcional*): UUID do agente.
  - `workflowId` (*string, opcional*): UUID do workflow associado.

#### 55. `create_reply_agent`
Cria um novo agente de resposta autônomo com persona e parâmetros de moderação.
- **Parâmetros**:
  - `name` (*string, obrigatório*): Nome do agente.
  - `personaPrompt` (*string, obrigatório*): Instruções de tom e comportamento.
  - `description` (*string, opcional*): Descrição do propósito do agente.
  - `minRankingScore` (*integer, opcional*): Nota mínima de relevância (padrão: 7).
  - `autoPublishReplies` (*boolean, opcional*): Publicar respostas sem aprovação manual (padrão: `true`).

#### 56. `update_reply_agent`
Atualiza a persona e configurações operacionais de um agente de resposta existente.
- **Parâmetros**:
  - `agentId` (*string, obrigatório*): UUID do agente.
  - `name` (*string, opcional*): Novo nome.
  - `personaPrompt` (*string, opcional*): Novo prompt de persona.
  - `autoPublishReplies` (*boolean, opcional*): Publicação automática.

#### 57. `delete_reply_agent`
Exclui um agente de resposta da biblioteca.
- **Parâmetros**:
  - `agentId` (*string, obrigatório*): UUID do agente.

#### 58. `assign_reply_agent`
Vincula ou desvincula um agente de resposta a um pipeline de workflow.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
  - `replyAgentId` (*string, opcional*): UUID do agente (ou nulo para desvincular).

#### 59. `configure_reply_agent`
Cria ou atualiza diretamente a configuração inline do agente de resposta de um workflow.
- **Parâmetros**:
  - `workflowId` (*string, obrigatório*): UUID do workflow.
  - `isEnabled` (*boolean, obrigatório*): Habilitar ou desabilitar o agente.
  - `personaPrompt` (*string, opcional*): Prompt de persona.
  - `autoPublishReplies` (*boolean, opcional*): Publicar automaticamente.

#### 60. `trigger_reply_check`
Dispara o ciclo de verificação de comentários novos nas plataformas e geração de respostas pela LLM.
- **Parâmetros**:
  - `workflowId` (*string, opcional*): UUID do workflow.
  - `agentId` (*string, opcional*): UUID do agente.
  - `postId` (*string, opcional*): UUID de um post específico.
  - `runSynchronously` (*boolean, opcional*): Aguardar retorno síncrono dos comentários avaliados.

---

### Domínio 9: Monitoramento, Auditoria e Engajamento

#### 61. `list_posts`
Lista posts publicados nas redes sociais (Bluesky, Twitter, Meta Threads, LinkedIn) com texto publicado real, métricas de engajamento (curtidas, reposts, respostas), comentários auditados e identificação da conta.
- **Parâmetros**:
  - `platform` (*string, opcional*): Filtrar por rede social.
  - `workflowId` (*string, opcional*): Filtrar por workflow.
  - `since` (*string, opcional*): Timestamp ISO-8601 inicial.
  - `hasReplies` (*boolean, opcional*): Filtrar apenas posts com respostas.
  - `limit` (*integer, opcional*): Quantidade máxima (padrão: 20).

#### 62. `assign_reply_agent_post`
Atribui um post específico para monitoramento contínuo por um agente autônomo de resposta.
- **Parâmetros**:
  - `agentId` (*string, obrigatório*): UUID do agente de resposta.
  - `postId` (*string, opcional*): UUID do post interno.
  - `externalPostUrl` (*string, opcional*): URL de um post externo para ingestão e monitoramento.

#### 63. `unassign_reply_agent_post`
Remove o monitoramento de um post de um agente de resposta.
- **Parâmetros**:
  - `agentId` (*string, obrigatório*): UUID do agente.
  - `postId` (*string, obrigatório*): UUID do post a desassociar.

#### 64. `list_reply_agent_posts`
Lista os posts atribuídos a um agente de resposta específico com métricas e contagens de comentários.
- **Parâmetros**:
  - `agentId` (*string, opcional*): UUID do agente de resposta.
  - `limit` (*integer, opcional*): Quantidade máxima (padrão: 50).

#### 65. `list_comment_audits`
Lista os comentários avaliados pelo agente de resposta com nota da LLM, decisão tomada, justificativa e respostas geradas.
- **Parâmetros**:
  - `workflowId` (*string, opcional*): Filtrar por workflow.
  - `agentId` (*string, opcional*): Filtrar por agente.
  - `decision` (*string, opcional*): Decisão (`Replied`, `SkippedBelowThreshold`, `SkippedStopCriteria`, `PendingApproval`, `Failed`).
  - `limit` (*integer, opcional*): Máximo de itens (padrão: 20).

#### 66. `reply_to_comment`
Aprova uma resposta pendente de auditoria ou envia uma resposta customizada diretamente a um comentário na rede social.
- **Parâmetros**:
  - `auditId` (*string, opcional*): UUID da auditoria pendente para aprovação.
  - `workflowId` (*string, condicional*): UUID do workflow se auditId omitido.
  - `postId` (*string, condicional*): UUID do post original se auditId omitido.
  - `commentId` (*string, condicional*): ID remoto do comentário na plataforma.
  - `replyText` (*string, opcional*): Texto da resposta.
  - `confirm` (*boolean, opcional*): Confirmação de envio público.

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

### 3.3 Twitter / X
- **Limite Fatal**: 280 caracteres por post no plano padrão.
- **Mídia**: Até 4 imagens ou 1 vídeo por post.

### 3.4 LinkedIn
- **Limite**: 3000 caracteres por post.
- **Suporte**: Posts de texto puro e threads com links informativos.

### 3.5 Tratamento de Erros no Servidor MCP
As respostas de erro seguem o padrão JSON-RPC:
- `-32602` (*Invalid Params*): Argumento ausente ou formato inválido.
- `-32601` (*Method Not Found*): Ferramenta não registrada.
- `-32000` (*Server Error*): Falha de banco de dados ou erro de execução capturado e logado.
