# Don't Trust Your Luck! — Development Plan

**Versão:** 0.1  
**Data:** 07/11/2026  
**Status:** plano proposto para aprovação  
**Projeto:** Unity 6 (`6000.6.8f1` no projeto atual, a confirmar), Universal 3D/URP, WebGL (Unity Web)  
**Documentos de referência:** `docs/GDD.md`, `docs/TECHNICAL_DESIGN.md`, `CLAUDE.md`  
**Responsável pela integração:** Claude Code principal, com revisão humana

> **Objetivo:** entregar uma partida completa, divertida, legível e executável no navegador **antes** de expandir o conteúdo artístico. Este plano não autoriza automaticamente mudanças de gameplay ainda não aprovadas.

---

## 1. Definição de sucesso

Uma versão submetível deverá:

1. Abrir por link no navegador desktop, sem instalação.
2. Permitir iniciar e concluir uma partida com pelo menos dois participantes (humanos locais ou bot, conforme decisão).
3. Apresentar tabuleiro numerado, dois dados, escolha de resultado, número da sorte e número do azar.
4. Resolver corretamente movimentação, vitória, eliminação e troca de turno/rodada.
5. Comunicar os estados por interface clara e feedback audiovisual mínimo.
6. Permitir reiniciar uma partida sem recarregar a página.
7. Não apresentar erros bloqueantes no console do navegador.
8. Ser publicado e testado na página de hospedagem escolhida.

**Metas, não garantias:** partidas de 5–10 minutos; alvo de 60 FPS, mínimo aceitável de 30 FPS estáveis em hardware-alvo a definir; carregamento suficientemente curto para uma game jam. Registrar medições reais.

### Prioridades MoSCoW

| Prioridade | Conteúdo |
|---|---|
| **Must** | Web build, tabuleiro, dois dados, escolha, sorte/azar, turnos, vitória, eliminação, HUD, reinício |
| **Should** | 2–4 jogadores, 1–2 bots, 4 cartas, feedback de áudio, personagens 2D provisórios |
| **Could** | Cena Fortaleza polida, animações adicionais, eventos especiais simples, seleção de personagem |
| **Won't (jam)** | Seis cidades, multiplayer online obrigatório, campanha, loja, progressão, cartas complexas |

**Regra de corte:** um item *Could* nunca bloqueia um item *Must*. Se o tempo apertar, cartas e bots podem ser reduzidos, desde que a partida continue jogável e as condições da competição sejam cumpridas.

---

## 2. Premissas e decisões pendentes (Gate D0)

Antes de codificar comportamentos irreversíveis, confirmar:

| ID | Decisão | Default apenas para protótipo | Quem valida | Status |
|---|---|---|---|---|
| D01 | Estrutura de turnos | Sequencial | Usuário + game-designer | Proposto |
| D02 | Participantes MVP | 2–4, controles locais | Usuário | Proposto |
| D03 | Índices do tabuleiro | 0 = início, 1–30 = casas; tabuleiro físico fixo, independente da sorte | Usuário | **Decidido (07/11/2026)** |
| D04 | Sorte | Fixa, sorteada de 15 a 30 | Usuário | Proposto |
| D05 | Azar | Um por rodada, sorteado em `1..Lucky−1`, revelado antes de qualquer jogada | Usuário | **Decidido (07/11/2026)** |
| D06 | Dados | Dois d6; escolher um resultado; escolha obrigatória mesmo se ambos levam ao azar | Usuário | **Decidido (07/11/2026)** |
| D07 | Exceder o limite | Rebote **no número da sorte**; vence apenas no destino final | Usuário | **Decidido (07/11/2026)** |
| D08 | Eliminação | Terminar na casa do azar; sem retroatividade | Usuário | Proposto |
| D09 | Último sobrevivente | Vence | Usuário | Proposto |
| D10 | Cartas | Reroll, Swap, Recuar −3, Shield | Usuário, antes de M2 | Pendente |
| D11 | Modo de teste solo | Hot-seat + bot simples já no M1 | Usuário | **Decidido (07/11/2026)** |
| D12 | Limite de rodadas / empate | 15 rodadas; vence o ativo mais próximo da sorte; empate na mesma casa compartilha a vitória | Usuário | **Decidido (07/11/2026)** |
| D13 | Ordem e rodada | Ordem fixa por `Player.Id`; eliminação no meio da rodada não ressorteia o azar | Usuário | **Decidido (07/11/2026)** |

Detalhes das decisões: GDD §3 e TDD §3, §7.2, §9.2.1.

**Bloqueio:** se uma tarefa depende de uma regra em aberto, o Claude deve registrar a pergunta e não inventar uma regra definitiva. Defaults podem ser usados em protótipo **somente após autorização explícita**.

---

## 3. Estratégia de execução

### Fases

- **F0 — Preparação:** diagnóstico, Git, documentos, configuração Web.
- **F1 — Núcleo:** domínio testável, tabuleiro, dados, sorte/azar.
- **F2 — Jogo completo:** turnos, UI, vitória, eliminação, reinício, build Web.
- **F3 — Interação:** cartas, bots e balanceamento, conforme tempo.
- **F4 — Arte e som:** vertical slice Tropical Folk Diorama.
- **F5 — Publicação:** compatibilidade, otimização, página e entrega.

### Marcos de aprovação

| Marco | Entregável | Critério para avançar |
|---|---|---|
| **M0** | Build Web mínimo | Abre no navegador, sem erro fatal |
| **M1** | Domínio testado | Regras principais passam em EditMode |
| **M2** | Partida completa com placeholders | Início → vitória/eliminação → reinício |
| **M3** | Build Web jogável | Partida concluída em navegador real |
| **M4** | Interação + identidade visual | Cartas/bots essenciais e cenário legível |
| **M5** | Release candidata | Teste da versão publicada, checklist aprovado |

**Milestone mais importante: M3.** Não iniciar polimento extenso antes dele.

---

## 4. Plano detalhado de tarefas

### Sprint 0 — Auditoria e fundação (F0)

**Meta:** preparar uma base confiável sem alterar o gameplay.

| ID | Tarefa | Agente líder | Dependência | Aceite |
|---|---|---|---|---|
| S0-01 | Ler GDD/TDD/CLAUDE.md e listar divergências | game-designer | — | Questões e defaults identificados |
| S0-02 | Auditar versão Unity, pacotes, URP, cenas e perfil Web | unity-developer | — | Relatório sem alterações |
| S0-03 | Verificar `.gitignore` e arquivos rastreados | unity-developer | — | `Library/`, `Temp/`, `Logs/`, `UserSettings/` ignorados |
| S0-04 | Validar riscos visuais WebGL | technical-artist | S0-02 | Recomendações de materiais e luz |
| S0-05 | Criar cena mínima de bootstrap e primeiro Web build | unity-developer | S0-02 + autorização | Abre via HTTP no navegador |
| S0-06 | Registrar baseline de testes | qa-tester | S0-05 | Checklist e evidências do smoke test |

**Observação:** não criar um segundo projeto Unity; trabalhar na estrutura já existente.

#### Definição detalhada da Sprint 0 (07/11/2026 — **executada**; M0 aprovado localmente, ver `docs/qa/S0-web-smoke-test.md`)

**Status das tarefas**

| ID | Status | Observação |
|---|---|---|
| S0-01 | Concluída | Divergências listadas no diagnóstico de 07/11/2026 (ver abaixo) |
| S0-02 | Concluída | Editor `6000.6.8f1`; WebGL instalado; perfil ativo `Web - Desktop - Development` (target WebGL, development, compressão `Disabled`); console sem erros |
| S0-03 | Concluída | `.gitignore` cobre `Library/`, `Temp/`, `Logs/`, `UserSettings/`; nenhum arquivo gerado versionado; Git LFS ativo para `*.png` |
| S0-04 | Concluída | Recomendações do technical-artist registradas (nível de qualidade Web dedicado, alpha clip, blob shadow) — aplicação fica para sprints futuras |
| S0-05 | Concluída | Cena Bootstrap + build Web Development (0 erros). Exigiu remover `com.unity.ai.inference` (plugin WebGL do `app-ui` quebrava a linkagem) |
| S0-06 | Concluída | Smoke test local aprovado; clique confirmado manualmente. Pendentes fora do M0: outros navegadores, canvas responsivo, itch.io |

**S0-05 — Bootstrap e primeiro build Web** (unity-developer)

| Arquivo | Ação |
|---|---|
| `Assets/_Game/Scenes/Bootstrap.unity` (+ `.meta`) | Criar pelo Editor: câmera, luz direcional, Canvas (Canvas Scaler *Scale With Screen Size* 1280×720), um botão e um texto, EventSystem com `InputSystemUIInputModule` |
| `Assets/_Game/Scripts/Presentation/SmokeTestButton.cs` (+ `.meta`) | Script mínimo: clique altera o texto e registra log. Sem regra de gameplay |
| `ProjectSettings/EditorBuildSettings.asset` | `Bootstrap` como cena 0; `SampleScene` desabilitada (não excluída) |
| `ProjectSettings/ProjectSettings.asset` | *Opcional, requer confirmação:* `defaultScreenWidthWeb/HeightWeb` = 1280×720 |
| `Builds/Web/` | Saída do build (ignorada pelo Git) |

**S0-06 — Baseline de QA** (qa-tester)

| Arquivo | Ação |
|---|---|
| `docs/qa/S0-web-smoke-test.md` | Checklist com versão Unity, perfil, compressão, navegador/versão, URL, resultado por passo e mensagens do console; itens não executados marcados como "não executado" |

**Dependências**
- Nenhum pacote novo é necessário para S0-05.
- Servidor HTTP local (`python -m http.server` ou `npx http-server`); compressão `Disabled` dispensa headers especiais. Nunca usar `file://`.
- *Recomendado, com autorização separada:* declarar `com.unity.test-framework` em `Packages/manifest.json` para destravar a Sprint 1.
- O teste em navegador depende de execução manual pelo usuário.

**Critérios de aceitação (M0)**
1. Projeto compila sem erros no console do Editor.
2. Build Web (perfil `Web - Desktop - Development`) conclui sem erros.
3. Página abre via `http://localhost` em pelo menos um navegador-alvo (Chrome, Firefox ou Edge).
4. Botão responde ao clique; console do navegador sem erro fatal.
5. UI utilizável em dois tamanhos de janela.
6. Relatório `docs/qa/S0-web-smoke-test.md` discrimina testes executados e não executados.
7. Nenhum commit sem autorização.

**Riscos conhecidos**

| Risco | Mitigação |
|---|---|
| Defines `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY` no perfil Web; pacotes `ai.*` podem falhar ou inflar o build | Tentar o build; se falhar, solicitar autorização para remover pacotes de IA (atenção: `com.unity.ai.assistant` fornece o Unity MCP) |
| Projeto dentro do OneDrive pode bloquear arquivos em `Library/`/`Temp/` | Decisão do usuário: mover o projeto ou pausar sincronização durante builds |
| Editor usa qualidade `PC`; build Web usa `Mobile` | Não aprovar visual pelo Editor; criar nível "Web" em sprint futura |
| Perfil Release usa Brotli sem fallback | Fora da Sprint 0; validar no itch.io (S5-03) |

**Divergências documentais identificadas (S0-01)**
- `docs/GDD.md` citado em CLAUDE.md, TDD e neste plano; arquivo real: `docs/GDD_Dont_Trust_Your_Luck_v0.2.md`.
- Numeração de marcos diferente entre TDD §2 (M0–M3) e este plano (M0–M5).
- D11 antecipa o bot para o M1, mas `S3-04` permanece na Sprint 3.
- Estrutura de pastas: GDD §10 (`Assets/Scripts/`) vs. CLAUDE.md/TDD §12 (`Assets/_Game/`).
- Nomes de cartas: GDD (Lucky Roll, Recuar −3) vs. TDD/plano (`reroll`, `move_back_3`).
- GDD exige "Unity 6 LTS"; não confirmado se `6000.6` é LTS.

### Sprint 1 — Domínio e tabuleiro (F1)

| ID | Tarefa | Agente líder | Dependência | Aceite |
|---|---|---|---|---|
| S1-01 | `MatchRulesConfig`, `MatchState`, `PlayerState` | unity-developer | D0 | Invariantes cobertas por testes |
| S1-02 | Gerador aleatório injetável e seed reproduzível | unity-developer | S1-01 | Teste com sequência conhecida |
| S1-03 | Sorteio de lucky/unlucky sem coincidência | unity-developer | S1-02 | Testes de limites |
| S1-04 | Cálculo de movimento e rebote | unity-developer | D07, S1-01 | Casos normais e bordas passam |
| S1-05 | Tabuleiro visual com 30 casas e âncoras | unity-developer | S1-01 | Índices visíveis, sem dependência da arte final |
| S1-06 | Revisão de regras e testes do domínio | qa-tester | S1-01…05 | Sem falhas críticas |

**Entrega:** o domínio calcula movimentos e resultados sem depender da posição visual de objetos Unity.

### Sprint 2 — Loop de partida (F2)

| ID | Tarefa | Agente líder | Dependência | Aceite |
|---|---|---|---|---|
| S2-01 | Máquina de estados, turnos e rodadas | unity-developer | Sprint 1 | Ações fora da fase rejeitadas |
| S2-02 | Dois dados, seleção e feedback | unity-developer | S2-01 | Uma escolha por turno |
| S2-03 | Vitória, azar, eliminação e último ativo | unity-developer | D08/D09, S2-01 | Testes de resolução |
| S2-04 | HUD: rodada, números, jogador, dados, mensagens | unity-developer | S2-02 | Legível em 1280×720 e janela menor |
| S2-05 | Reinício sem duplicação de estado/listeners | unity-developer | S2-03 | Duas partidas seguidas sem erros |
| S2-06 | Testes EditMode/PlayMode + build Web jogável | qa-tester + unity-developer | S2-01…05 | **M3 aprovado** |

**Entrega:** primeira partida integralmente jogável no navegador com placeholders.

### Sprint 3 — Estratégia e social (F3)

**Executar apenas após M3 e aprovação das regras de cartas.**

| ID | Tarefa | Agente líder | Dependência | Aceite |
|---|---|---|---|---|
| S3-01 | Formalizar timing, alvo e prioridade das quatro cartas | game-designer | D10 | Tabela de regras aprovada |
| S3-02 | `CardDefinition` e resolvedor de efeitos | unity-developer | S3-01 | Cartas não modificam UI diretamente |
| S3-03 | Implementar cartas em ordem: Recuar, Reroll, Shield, Swap | unity-developer | S3-02 | Teste de cada interação |
| S3-04 | Bot simples usando comandos normais | unity-developer | S2 | Conclui partida sem intervenção humana |
| S3-05 | Playtests e ajuste de ritmo | game-designer + qa-tester | S3-03/04 | Registro de duração, problemas e ajustes |

**Corte:** se houver pouco tempo, implementar **uma ou duas cartas muito bem testadas** em vez de quatro incompletas.

### Sprint 4 — Vertical slice artístico (F4)

| ID | Tarefa | Agente líder | Dependência | Aceite |
|---|---|---|---|---|
| S4-01 | Guia de assets: escala, pixel density, paleta, materiais | technical-artist | M3 | Convenções documentadas |
| S4-02 | Ambiente compacto inspirado em Fortaleza | technical-artist | S4-01 | Caminho e casas sempre legíveis |
| S4-03 | Dois a quatro animais 2D em recortes de papel | technical-artist | S4-01 | Sprites visíveis de todos os ângulos de câmera usados |
| S4-04 | HUD de cartas envelhecidas e estados de sorte/azar | technical-artist | S2-04 | Contraste e responsividade preservados |
| S4-05 | Áudio, transições e feedback essencial | unity-developer + technical-artist | S2 | Sem travamento e sem dependência de autoplay |
| S4-06 | Perfil de desempenho no navegador | qa-tester | S4-02…05 | Problemas medidos e priorizados |

**Regra:** arte não altera lógica do domínio. Referências a artistas orientam criações originais, sem reproduzir obras protegidas.

### Sprint 5 — Estabilização e publicação (F5)

| ID | Tarefa | Agente líder | Dependência | Aceite |
|---|---|---|---|---|
| S5-01 | Testes de regressão e partidas repetidas | qa-tester | M4 | Sem bloqueadores |
| S5-02 | Build de distribuição Web | unity-developer | S5-01 | Abre em HTTP e sem erros críticos |
| S5-03 | Testar compressão e hospedagem HTML5 no itch.io | unity-developer | S5-02 | Página publicada carrega corretamente |
| S5-04 | Verificar Chrome/Firefox/Edge, tamanhos de janela e áudio | qa-tester | S5-03 | Evidências registradas |
| S5-05 | Capturas, descrição, controles, créditos e licença de assets | game-designer + technical-artist | S5-03 | Página pronta para jurados |
| S5-06 | Congelamento de escopo e tag de release | Claude principal, com autorização | S5-01…05 | `v0.1.0-jam` somente se aprovado |

**Buffer:** reservar a etapa final exclusivamente para bugs, publicação e contingências; não iniciar features novas nesse período.

---

## 5. Cronograma comprimido de sete dias

> **Referência relativa**, não confirmação da duração oficial da Unity Game Jam. Ajustar às datas e regras reais do evento.

| Dia | Prioridade absoluta | Entrega ao fim do dia | Corte se atrasar |
|---|---|---|---|
| **D1** | Auditoria, decisões D0, Git e Web build vazio | M0 | Menus elaborados |
| **D2** | Domínio, tabuleiro, sorte/azar e movimento | M1 | Efeitos visuais |
| **D3** | Turnos, dados, vitória, eliminação, HUD | M2 | Cartas |
| **D4** | Teste completo e Web build jogável | **M3** | Bots avançados |
| **D5** | Cartas essenciais, bot opcional, testes de ritmo | Parte de M4 | Cartas extras |
| **D6** | Cenário, sprites, HUD final, áudio e otimização | M4 | Cenários adicionais |
| **D7** | QA, publicação, testes externos, correções | **M5** | Qualquer feature nova |

**Se M3 não for alcançado até D4:** congelar conteúdo adicional e priorizar apenas a partida básica e a publicação.

---

## 6. Dependências e paralelismo dos agentes

```text
GDD + TDD + CLAUDE.md
          │
          ▼
    D0 + Auditoria
          │
          ▼
    Web build vazio (M0)
          │
          ▼
 Domínio + testes (M1)
          │
          ▼
 Partida jogável (M2)
          │
          ▼
   Web jogável (M3)
       ┌──┴─────┐
       ▼        ▼
  Cartas/bots  Guia artístico
       └──┬─────┘
          ▼
    Integração (M4)
          │
          ▼
    Release Web (M5)
```

### Papel dos agentes

| Agente | Faz | Não faz sem autorização |
|---|---|---|
| **game-designer** | Especifica regras, testa hipóteses, aponta ambiguidades | Alterar regras aprovadas ou código |
| **unity-developer** | Implementa domínio, Unity, UI e build | Inventar regras, instalar pacotes arbitrários |
| **technical-artist** | Define pipeline, assets, iluminação e otimização visual | Substituir sistemas de gameplay |
| **qa-tester** | Revisa critérios, testes e regressões | Declarar teste executado sem evidência |
| **Claude principal** | Decompõe tarefas, delega, integra, relata e solicita decisões | Criar commits, mudar escopo ou publicar sem autorização |

**Economia de tokens:** delegar tarefas com entradas/saídas delimitadas; evitar quatro agentes lendo o repositório inteiro repetidamente. Preferir execução sequencial em arquivos compartilhados; paralelismo apenas em áreas sem conflito.

---

## 7. Git, branches e commits

### Estratégia

- `main`: estado integrável e, idealmente, jogável.
- Branches curtas por funcionalidade (`feat/board-domain`, `feat/luck-system`, `feat/game-hud`, `art/fortaleza-scene`, `fix/web-build`).
- Para equipe pequena e prazo curto, branches são **opcionais**; não sacrificar tempo de entrega com fluxo burocrático.
- Commits pequenos, descritivos e após verificações relevantes.
- Não versionar `Library/`, `Temp/`, `Logs/`, `Obj/`, `Build/`, `Builds/`, `UserSettings/` ou credenciais.
- Versionar `Assets/**/*.meta`, `Packages/` e `ProjectSettings/`.
- Evitar projeto dentro de pasta sincronizada pelo OneDrive.

### Sequência sugerida

```text
chore: initialize Unity URP project
chore: configure Git and Web build baseline
docs: add GDD and technical design
docs: add development plan
chore: configure Claude Code agents
feat: add match domain models and rules config
test: cover board movement and edge cases
feat: implement lucky and unlucky number system
feat: add turn and dice selection flow
feat: implement win and elimination conditions
feat: add playable board HUD
fix: resolve Web build compatibility issues
feat: add core action cards
feat: add basic bot opponent
art: implement first tropical folk diorama scene
polish: add audio and gameplay feedback
chore: prepare jam release build
```

A ordem pode variar conforme o repositório atual. **Nunca recriar commits já realizados só para seguir a lista.** Claude Code deve solicitar autorização antes de executar `git commit`, `git push` ou criar tags.

### Convenção de commit

`tipo: descrição curta` (`feat`, `fix`, `test`, `docs`, `chore`, `art`, `polish`). Uma funcionalidade e seus testes podem ficar no mesmo commit quando fizer sentido.

---

## 8. Fluxo de trabalho com Claude Code

### Antes de cada tarefa

1. Ler instruções e arquivos relevantes, sem reabrir todos os documentos desnecessariamente.
2. Inspecionar o estado do repositório (`git status`) e dependências existentes.
3. Explicar objetivo, arquivos afetados e critérios de aceitação.
4. Identificar regras pendentes e pedir aprovação quando necessário.
5. Implementar o menor incremento verificável.

### Depois de cada tarefa

1. Executar testes possíveis; registrar comandos e resultados.
2. Informar o que **não** pôde ser testado no Editor ou Web.
3. Resumir arquivos alterados e riscos.
4. Propor próximo passo; não iniciar outra sprint automaticamente.
5. Solicitar aprovação para commit, se apropriado.

### Prompt inicial recomendado (sem modificações)

```text
Leia CLAUDE.md, docs/GDD.md, docs/TECHNICAL_DESIGN.md e
 docs/DEVELOPMENT_PLAN.md. Inspecione a estrutura atual do
 projeto Unity, a versão do Editor, os pacotes, o URP, as
 configurações de Web build e o estado do Git.

Delegue aos agentes especializados apenas as análises
 pertinentes. Entregue: (1) diagnóstico, (2) bloqueadores,
 (3) decisões de gameplay ainda pendentes, (4) plano mínimo
 para M0 com arquivos afetados e critérios de aceitação.

Não modifique arquivos, não instale pacotes, não crie commits
 e não inicie implementação.
```

### Prompt para executar Sprint 0 após aprovação

```text
Vamos executar somente a Sprint 0 do DEVELOPMENT_PLAN.md.
Antes de alterar arquivos, apresente as tarefas, dependências,
 arquivos afetados e riscos. Aguarde minha aprovação.
Depois implemente em incrementos pequenos, preserve os
 arquivos Unity existentes e verifique o primeiro build Web.
Não crie commits nem publique nada sem minha autorização.
```

---

## 9. Plano de testes e telemetria manual

### Testes automáticos prioritários

- Sorte e azar nunca coincidem.
- Dois dados entre 1 e 6, escolha válida apenas uma vez.
- Movimento simples, rebote, casa inicial e última casa.
- Vitória apenas no destino final.
- Eliminação somente quando regra é acionada; sem retroatividade.
- Jogador eliminado não recebe turno.
- Ordem de turnos e mudança de rodada.
- Reinício não mantém estado nem listeners antigos.
- Seed fixa reproduz sorteios.
- Comandos inválidos não alteram estado.
- Cartas não permitem ações fora da fase ou sem posse.

### Sessões de playtest

| Sessão | Momento | Pergunta principal |
|---|---|---|
| P1 | Após M2 | As regras são compreensíveis sem explicação longa? |
| P2 | Após M3 | A partida é divertida e termina em tempo razoável? |
| P3 | Após cartas | As escolhas importam ou a sorte domina demais? |
| P4 | Antes da release | O jogo funciona e é legível para alguém de fora da equipe? |

Registrar: duração, número de turnos, eliminações precoces, cartas usadas, situações confusas, bugs e sugestões. Não inventar resultados de playtests.

### Checklist Web

- [ ] Módulo Web Build Support instalado.
- [ ] Build Web gerado sem erros.
- [ ] Executado via HTTP local, não `file://`.
- [ ] Console do navegador sem erros bloqueantes.
- [ ] Mouse/teclado respondem corretamente.
- [ ] Uma partida inteira pode ser concluída.
- [ ] Reinício funciona.
- [ ] HUD legível em 1280×720 e janela menor.
- [ ] Áudio funciona após interação do usuário.
- [ ] Página publicada no itch.io testada.
- [ ] Créditos e licenças de assets revisados.

---

## 10. Registro de riscos

| Risco | Impacto | Mitigação | Gatilho de corte |
|---|---|---|---|
| Build Web falha tarde | Crítico | Primeiro build no M0, novo build em M3 | Falha persistente bloqueia novas features |
| Regras indefinidas | Alto | Gate D0, aprovação por sistema | Não implementar efeitos ambíguos |
| Multiplayer online excede prazo | Alto | Local primeiro, rede desacoplada | Não iniciar online na jam |
| Eliminação precoce gera espera | Médio/alto | Partidas curtas e playtest | Reduzir jogadores/ajustar regras |
| Cenário compromete legibilidade | Alto | Tabuleiro com contraste e placeholders | Remover decoração obstrutiva |
| URP pesado no navegador | Alto | Iluminação estática, texturas menores, profiling | Cortar efeitos e partículas |
| Agentes alteram mesmos arquivos | Médio | Um integrador, tarefas delimitadas | Suspender paralelismo |
| Arte demanda mais tempo | Alto | Vertical slice e assets modulares | Manter placeholders polidos |
| Falta de testes reais | Crítico | QA e smoke test por marco | Congelar features |
| Dependência de assets externos | Médio | Licenças verificadas, fallback próprio | Remover asset sem licença clara |

---

## 11. Critérios de conclusão por entrega

Uma tarefa é considerada **Done** quando:

- [ ] Implementação corresponde ao requisito aprovado.
- [ ] Código e cenas não apresentam erros conhecidos bloqueantes.
- [ ] Testes relevantes foram executados ou sua impossibilidade foi declarada.
- [ ] Alterações não quebram o loop de partida.
- [ ] Arquivos e decisões foram documentados quando necessário.
- [ ] Estado do Git está claro; não há arquivos gerados versionados por acidente.
- [ ] O responsável informa como reproduzir e validar a funcionalidade.

**Para marcos M0, M3 e M5, build e teste em navegador são obrigatórios.** A aprovação final cabe ao usuário.

---

## 12. Próxima ação concreta

1. Colocar este arquivo em `docs/DEVELOPMENT_PLAN.md`.
2. Abrir Claude Code na raiz do projeto Unity.
3. Enviar o **prompt inicial recomendado** da seção 8.
4. Analisar o diagnóstico e aprovar as decisões D01–D09 essenciais.
5. Autorizar somente a **Sprint 0**, com primeiro build Web.

> **Princípio de produção:** *jogabilidade primeiro, compatibilidade Web desde o primeiro dia e arte final somente sobre um núcleo validado.*
