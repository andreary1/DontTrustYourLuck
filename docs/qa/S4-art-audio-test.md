# S4 — Direção de arte, áudio e feedback: relatório de testes

**Data:** 09/11/2026
**Sprint:** 4 (S4-01 … S4-05; S4-06 não executado)
**Branch:** `main` (alterações não commitadas)
**Resultado geral:** os testes automatizados do Editor passaram. O build Web de desenvolvimento foi gerado sem erros. O smoke test no navegador foi **parcial**: carregamento, entrada, cartas, bots, troca de turno/rodada e popover de som foram verificados. A partida completa, a tela de vitória/derrota e o reinício **não** foram verificados no navegador, porque o painel de navegador disponível rodava a ~1 quadro/s. Nenhuma métrica de desempenho foi registrada.

## Ambiente

| Item | Valor |
|---|---|
| Unity | `6000.6.8f1` (Editor aberto pelo usuário) |
| Execução dos testes | `TestRunnerBridge.RunEditMode()` / `RunPlayMode()` → `Temp/TestRun_results.txt` |
| Builder | `Tools > Don't Trust Your Luck > Build Sprint 4 Art Direction (All)` (`Sprint4Builder.BuildFromCommandLine`) |
| Build Web | `BuildPipeline.BuildPlayer`, `BuildTarget.WebGL`, `BuildOptions.Development`, saída `Builds/Web` |
| Navegador | Painel de navegador integrado do app Claude (Chromium), servidor `python -m http.server 8080` |

## Testes automatizados

| Suíte | Resultado |
|---|---|
| EditMode (Sprints 1–4) | **169 passou, 0 falhou, 0 ignorado, 0 inconclusivo** |
| PlayMode (Sprints 2–4) | **14 passou, 0 falhou, 0 ignorado, 0 inconclusivo** |

As duas suítes foram executadas de novo depois das correções finais de QA (assinatura tardia do `MusicController` e fallback de texto do `VisualFeedbackController`), com o mesmo resultado.

### EditMode — `Tests/EditMode/Presentation/Sprint4PresentationTests.cs` (15 testes)

- `AudioSettingsModel`: valores padrão; carga com clamp; volumes efetivos e mute; evento `Changed` e persistência (store em memória).
- `AudioEventRouter.MapEvent`: eventos centrais e efeitos de cartas mapeados para `SfxId`; `RoundStarted` sem som próprio.
- `BoardCameraRig.ComputeOffCenterExtents`: caso simétrico e mapeamento da área segura.
- Peças: `SetCharacter` ajusta proporção do recorte (`Visual/Card`); a raiz da peça nunca se move por animação; pose de eliminado e escala de vencedor; escudo limpo ao eliminar.
- Tabuleiro: material `beyondLucky` sem alterar índices; marcadores acompanham destaques e somem quando não há destaque.

### PlayMode — `Tests/PlayMode/Sprint4FeedbackPlayModeTests.cs` (5 testes)

- Partida padrão mostra onça/capivara/macaco/tucano; marcadores de sorte e azar sobre as casas certas; 31 casas com índices 0–30.
- Partida só de bots toca os sons principais (`LuckyChime`, `DangerReveal`, `TurnChange`, `DiceRoll`, `DieChoose`, movimento, fim de partida); a seta ativa fica presa à peça do turno e some no fim.
- Sliders e mute de áudio controlam o modelo, e o modelo atualiza a view sem laço de feedback.
- Turno humano mostra o banner.
- Reinício volta as peças ao visual normal, sem escudo, na casa 0.

Os testes PlayMode salvam e restauram as configurações reais de áudio (`PlayerPrefs`) no `TearDown`.

## Build Web

| Item | Valor |
|---|---|
| Resultado | `Succeeded` |
| Erros / avisos | 0 / 0 |
| Tamanho reportado | 60.979.725 bytes (build **Development**, sem compressão) |
| Tempo | 00:04:01 |
| Cena | `Assets/_Game/Scenes/GameBoard.unity` |

O build foi gerado **antes** das duas correções finais de QA, que só afetam a apresentação (fallback nulo e assinatura de evento). Não foi refeito.

O build alterou `m_WebGLTextureSubtarget` (1 → 0) em `Assets/Settings/Build Profiles/Web - Desktop - Development.asset`. O arquivo foi restaurado com `git checkout` para não mudar configuração do projeto sem autorização.

## Smoke test no navegador (parcial)

| Verificação | Resultado |
|---|---|
| Carregamento da página e do jogo | ✅ Carregou; diorama, HUD, 31 casas, sol na casa da sorte, olho na casa do azar, standees dos 4 animais |
| Áudio após o primeiro clique | ✅ Console: `Audio context resumed after 0.772 seconds.` (autoplay respeitado). A audição em si não foi verificada |
| Rolar dados (clique) | ✅ Dados exibidos com prévia de destino (`-> 1`, `-> 4`) |
| Escolher dado e mover | ✅ Peça move; log e HUD atualizados |
| Carta Shield | ✅ Status "Shielded", aura visível; "Card already used this turn."; escudo expira após movimento seguro |
| Lucky Roll antes de rolar | ✅ "No card can be played now." |
| Turnos dos bots e troca de rodada | ✅ Rodadas 1 → 3, novo número do azar a cada rodada |
| Popover de som | ✅ Abre com Master/Music/Effects e Mute all |
| Console | ✅ 0 erros. Há logs informativos esperados: `Trying to get length of sound which is not loaded yet.` (carregamento assíncrono de áudio no Web) e shaders de pós-processamento URP removidos do build (DOF, Panini, FSR) |
| Partida completa, vitória/derrota, Play Again | ❌ **Não executado** (ver limitação) |
| Redimensionamento real da janela | ⚠️ Parcial: com o painel em 583×486, o template `Default` mantém o canvas fixo em 960×600 e aparecem barras de rolagem |

### Limitação do ambiente

`requestAnimationFrame` mediu **~1 quadro/s** na aba do jogo e **~5 quadros/s numa aba vazia** do mesmo servidor. O painel estava sendo limitado pelo ambiente, não pelo jogo. Por isso:

- a partida completa não foi levada até o fim no navegador;
- **nenhum número de FPS, memória ou tempo de carregamento é reportado** (S4-06 fica pendente).

Partida completa e reinício estão cobertos pelos testes PlayMode no Editor, mas isso **não** substitui o teste em navegador.

## Auditoria (qa-tester) — "Aprovado com ressalvas"

Diff de `Domain/` e `Infrastructure/` vazio: nenhuma regra de jogo foi alterada.

| Achado | Status |
|---|---|
| `MusicController` só assinava `Settings.Changed` no `OnEnable` | **Corrigido**: assinatura também no `Start`, protegida por flag |
| `VisualFeedbackController` podia gerar NRE sem `hud` | **Corrigido**: fallback para `HudText` padrão |
| `AudioSettingsModel.Commit` chama `PlayerPrefs.Save()` a cada passo do slider | Aberto: no Web grava no IndexedDB; sugerido salvar ao soltar o slider |
| Janela de `IsRolling` quando um reinício interrompe a rolagem | Aberto: autocorrige em ~0,5 s |
| `catch` silencioso em `ComputeDestinations` | Aberto: adicionar log |
| `preloadAudioData` desativado só na música; voz única com pitch compartilhado no `PlayOneShot` | Aberto, baixo impacto |
| Cena usa `persistSettings` real; testes dependem de restaurar `PlayerPrefs` | Mitigado no `TearDown`; sugerido `persist:false` em testes |
| Lacunas de teste: eliminado com escudo, seta quando o jogador do turno é eliminado, banner de bot, áudio mudo, cooldown, ducking, reinício no meio da rolagem, reativação do router | Abertos |

## Pendências para Sprint 5

1. Template Web responsivo, ou tamanho 1280×720 para o embed no itch.io (configuração de projeto, requer autorização).
2. Perfil de desempenho (S4-06) num navegador real em primeiro plano: FPS, memória, tempo de carregamento e build Release comprimido.
3. Partida completa + vitória/derrota + Play Again no navegador.
4. Substituir a arte e o áudio provisórios (lista em `docs/ART_DIRECTION.md`).
