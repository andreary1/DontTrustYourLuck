# S3 — Sistema de cartas: relatório de testes

**Data:** 08/11/2026
**Sprint:** 3 (S3-01 … S3-03, HUD de cartas, arte provisória)
**Branch:** `feat/sprint3-cards` (a partir de `origin/main` com a Sprint 2)
**Resultado geral:** regras, integração e HUD validados por testes automatizados no Editor. Build Web **não executado** nesta etapa.

## Ambiente

| Item | Valor |
|---|---|
| Unity | `6000.6.8f1` (Editor aberto pelo usuário via Hub) |
| Execução | Menu `Tools > Don't Trust Your Luck` (`TestRunnerBridge`) |
| Builder | `Build Sprint 3 Cards (Art + Assets + HUD)` — log: `[Sprint3CardsBuilder] Card art, definitions, config and HUD updated.` |

## Testes automatizados

| Suíte | Resultado |
|---|---|
| EditMode (Sprints 1–3) | **154 passou, 0 falhou** |
| PlayMode (`GameBoardMatchPlayModeTests` + `CardHudPlayModeTests`) | **9 passou, 0 falhou, 0 inconclusivo** |

Antes da execução no Unity, os 141 testes EditMode de domínio também foram compilados com o Roslyn do Unity e executados num runner de reflexão sobre .NET 8 (141/141; um teste-canário com falha proposital foi detectado corretamente). Todo o código Runtime/Editor/PlayMode foi verificado por compilação contra as DLLs do Unity 6000.6.8f1.

### EditMode — `Tests/EditMode/Domain/Cards/CardRulesTests.cs`

- **Lucky Roll:** só após a rolagem; substitui os dois dados (eventos `CardPlayed → DiceRerolled → CardConsumed`); antes da rolagem é rejeitada sem consumir carta nem RNG; consumo correto (a outra cópia permanece); segunda carta no turno bloqueada; escolha usa os novos dados e não há segundo movimento.
- **Swap:** troca correta; rejeita a si mesmo, eliminado, id inexistente e ausência de alvo sem consumir; troca para a casa do azar não elimina nem vence; alvo na mesma casa é válido e consumido (D10); após a rolagem é rejeitada; turno continua e o movimento parte da nova casa.
- **Retreat:** 10→7, 3→0, 2→0, 0→0 (válida e consumida); recuar para a casa do azar não elimina; turno continua; movimento seguinte parte da casa nova; alvo é rejeitado.
- **Shield:** ativação antes da rolagem; bloqueia eliminação (jogador fica na casa, ordem `PlayerMoved → ShieldConsumed → TurnChanged`); consumida ao bloquear; expira após movimento seguro; rejeitada após a rolagem; não persiste entre turnos; não protege outros; vitória exata com Shield; vitória por sobrevivência preservada.
- **Gerais:** mão inicial de 2 (sorteio do stream de cartas, ids únicos, duplicatas possíveis); limite de 3 (`HandFull`); concessão rejeita carta inexistente/eliminado; reprodutibilidade por seed e cartas não alteram sorte/azar/dados; jogadas inválidas não consomem; uma carta por turno com reinício do limite; cartas de outro jogador rejeitadas; eliminado, partida encerrada e cartas desativadas rejeitados; `ValidateCardPlay` sem efeitos colaterais; reinício limpa mãos/escudos/limite; sessão publica eventos e reporta rejeições; 25 partidas só de bots com cartas terminam; fluxo só de dados inalterado; catálogo rejeita ids duplicados.
- Testes da Sprint 2 (motor, sessão, bots) continuam passando sem alteração.

### PlayMode — `Tests/PlayMode/CardHudPlayModeTests.cs`

- Exibição da mão do jogador atual (nome do asset, disponibilidade antes da rolagem, terceiro slot oculto).
- Lucky Roll desabilitada antes de rolar, habilitada depois; dados do HUD atualizados; mensagem "New dice"; segunda carta indisponível.
- Swap: alvos elegíveis (só outros ativos), Play bloqueado sem alvo, **Cancelar não consome**, troca move as peças e atualiza posição no HUD; turno continua.
- Retreat anima e posiciona a peça; Shield mostra status "Shielded" e expira após o movimento.
- Cartas desabilitadas no turno do bot; reinício limpa seleção e redistribui mãos sem duplicar slots.
- Partida completa (humano usando cartas a cada turno + 3 bots) termina sem bloqueio e mostra a tela de resultado.

## Não executado

- **Build Web e teste no navegador** (carregar, cartas com mouse real, partida completa, reinício, console).
- Inspeção visual do layout do painel de cartas em 1280×720 e em janelas menores (os testes verificam estado, não aparência).
- Medição de FPS/memória.

## Observações

1. O Editor reserializou `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` (`m_RuntimeSettings` ficou vazio). Não foi alterado pelo código da Sprint 3; avaliar antes de commitar (pode afetar o build Web).
2. O batchmode do Unity é recusado nesta máquina (`com.unity.editor.headless` / token de licença indisponível); builds e testes precisam do Editor aberto pelo Hub.
