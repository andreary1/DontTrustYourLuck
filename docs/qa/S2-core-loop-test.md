# S2 — Core Gameplay Loop: relatório de testes

**Data:** 08/11/2026
**Sprint:** 2 (S2-01 … S2-06)
**Resultado geral:** loop completo funcional no Editor (testes automatizados) e no build Web (partida manual automatizada no navegador embutido). Pendências abaixo.

## Ambiente

| Item | Valor |
|---|---|
| Unity | `6000.6.8f1` |
| Build Profile | `Web - Desktop - Development` (WebGL 2, compressão `Disabled`) |
| Cena 0 | `Assets/_Game/Scenes/GameBoard.unity` (Bootstrap desabilitada na lista) |
| Servidor | `python -m http.server 8080 --bind 127.0.0.1 --directory Builds/Web` |
| Navegador | Navegador embutido do app Claude (Chromium; versão não registrada) |

## Testes automatizados

Executados via `Tools > Don't Trust Your Luck > Run EditMode/PlayMode Tests` (`TestRunnerBridge`, resultado em `Temp/TestRun_results.txt`).

| Suíte | Resultado |
|---|---|
| EditMode (Sprint 1 + Sprint 2) | 117 passou, 0 falhou |
| PlayMode (`GameBoardMatchPlayModeTests`) | 3 passou, 0 falhou |

Cobertura EditMode da Sprint 2 (`Tests/EditMode/Domain/Match/`): início válido e inválido (quantidade, ids duplicados), sorteio único do Lucky, Unlucky por rodada, alternância por Id, bloqueio de comandos fora da fase/turno sem consumir RNG, rolagem de dois dados, escolha (sem soma), índice inválido, rolagem/escolha duplicada, rebote, vitória exata, passar pelo Lucky sem vencer, eliminação, passar pelo Unlucky sem eliminar, sem eliminação retroativa, eliminados fora dos turnos, último sobrevivente, comandos bloqueados após o fim, limite de rodadas com empate compartilhado, reinício completo, bot (vitória, evitar azar, mais perto do Lucky, desempate), bot pela mesma API, 25 partidas só de bots terminando, reprodutibilidade por seed, limite de rodadas ignorando eliminados, último sobrevivente antes do limite de rodadas, ids não contíguos, ids ≤ 0 rejeitados.

Cobertura PlayMode: botões por fase, cliques ignorados no turno do bot e durante a animação, reinício no meio da animação, `BeginMatch` duplo sem duplicar peças, valores dos dados no HUD, botões desabilitados durante a resolução, peça igual à posição lógica, destaque e troca do jogador ativo, rodada/azar no HUD, partida só de bots até o fim, tela de resultado, Play Again sem peças/HUD/EventSystem duplicados, bots continuando após o reinício.

## Smoke test no navegador (build Web Development, 08/11/2026)

| # | Passo | Resultado |
|---|---|---|
| 1 | Carregar página | ✅ Sem erros no console |
| 2 | HUD inicial (Lucky, Unlucky, rodada, turno, jogadores) | ✅ |
| 3 | Partida 1 humano + 3 bots até o fim | ✅ Jaguar (humano) venceu na rodada 5 com chegada exata no Lucky 15 |
| 4 | Bots jogam automaticamente | ✅ |
| 5 | Rebote exibido | ✅ ("Capybara bounced back to tile 13") |
| 6 | Tela de resultado + Play Again sem recarregar | ✅ Nova partida (Lucky 30), tudo zerado |
| 7 | Segunda partida | ⚠️ Jogada até a rodada 2, **não concluída** no navegador (ambiente renderizava ~1 fps sob automação) |
| 8 | Eliminação visível no navegador | ⚠️ Não ocorreu nessa partida; coberta por testes EditMode/PlayMode |
| 9 | Console após as partidas | ✅ Sem erros |
| 10 | Build final (após ajustes de layout do HUD e correções do QA) | ✅ Carrega, HUD inicial correto (Lucky/Unlucky/rodada/jogadores), console sem erros. Partida completa **não repetida** nesse build. |

## Observações

1. **Cliques automatizados:** como na Sprint 0, cliques instantâneos às vezes não chegam ao `InputSystemUIInputModule`. Cliques com pressão mais longa funcionaram sempre. Não afeta mouse real; falta confirmar com teste manual.
2. **Taxa de quadros não medida:** o painel do navegador rodava ~1 fps sob automação (medido via `requestAnimationFrame`), por limitação do ambiente. Nenhum valor de FPS, memória ou carregamento foi registrado.
3. **Canvas fixo 960×600** (template `Default`). Em janelas menores que o canvas, parte do jogo fica fora da tela. Pendente de decisão (template responsivo ou `defaultScreenWidthWeb/HeightWeb`).
4. Bug encontrado e corrigido no teste: o status "X is thinking..." aparecia durante a animação do humano. O status passou a usar o jogador exibido.

## Não executado

- Partida completa no build final (só carregamento + HUD verificados; o loop foi validado no build anterior e nos testes PlayMode).

- Partida manual com mouse real (usuário).
- Firefox, Edge e Chrome standalone; itch.io; fullscreen.
- Medição de FPS, memória e tempo de carregamento.

## Como reproduzir

1. Abrir `GameBoard.unity` e dar Play (ou gerar o build com o perfil `Web - Desktop - Development` em `Builds/Web`).
2. Servir `Builds/Web` por HTTP local e abrir `http://localhost:8080`.
3. Jogar como Jaguar: `Roll Dice`, depois `Choose A/B`. Os bots jogam sozinhos.
4. Ao fim, clicar em `Play Again` e jogar outra partida.
