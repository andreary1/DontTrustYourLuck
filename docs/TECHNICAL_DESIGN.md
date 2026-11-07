# Don't Trust Your Luck! — Technical Design Document

**Versão:** 0.1  
**Data:** 07/11/2026  
**Status:** arquitetura proposta; regras pendentes não são decisões aprovadas  
**Documentos relacionados:** `docs/GDD.md`, `CLAUDE.md`  
**Plataforma principal:** Unity Web (WebGL), publicação planejada no itch.io

---

## 1. Objetivo e princípios

Este documento especifica **como implementar** o protótipo de *Don't Trust Your Luck!* na Unity. O GDD define **o que o jogo deve ser**; este TDD define os componentes, contratos, fluxos, dados, testes e restrições técnicas necessários para construí-lo.

**Princípios obrigatórios:**
1. **WebGL-first:** o jogo deve funcionar em navegador desktop, não somente no Editor.
2. **Núcleo determinístico:** regras e estado da partida independentes de `MonoBehaviour`, cena, animação, áudio e rede.
3. **Autoridade única sobre o estado:** a interface solicita ações; somente o motor de regras altera o estado.
4. **Vertical slice antes do polimento:** primeiro uma partida completa com placeholders; depois cartas, bots e arte.
5. **Baixo acoplamento:** a lógica não deve conhecer a UI, os prefabs nem o transporte de rede.
6. **Decisões explícitas:** valores ainda não aprovados são parâmetros de protótipo, não regras definitivas.
7. **Mudanças incrementais:** o Claude Code deve inspecionar o projeto real antes de criar scripts, cenas ou pacotes.

### 1.1 Metas de produto

| Aspecto | Meta inicial |
|---|---|
| Engine | Unity 6; confirmar versão em `ProjectSettings/ProjectVersion.txt` |
| Pipeline | URP, template Universal 3D |
| Plataforma | Web / WebGL 2 |
| Resolução de referência | 1280 × 720, UI responsiva |
| Input | Mouse e teclado; compatibilidade com navegador |
| Partida | 2–4 jogadores no MVP (proposta) |
| Tabuleiro | Percurso linear de 30 casas (proposta) |
| Duração | 5–10 minutos (meta, sujeita a testes) |
| Primeiro cenário | Placeholder; Fortaleza como direção artística proposta |
| Distribuição | Build Web em página itch.io |
| Meta de desempenho | Buscar 60 FPS; aceitar 30 FPS estáveis em hardware-alvo definido |

**Não presumir** que a instalação do módulo Web Build Support, a configuração do URP ou a compatibilidade dos pacotes já estejam corretas: verificar no projeto.

---

## 2. Escopo e sequência

### 2.1 Marco M0 — Fundação técnica

- Projeto Unity abre sem erros.
- Repositório Git configurado com `.gitignore` para Unity.
- `CLAUDE.md`, `docs/GDD.md` e este documento presentes.
- Perfil de build **Web** disponível e selecionável.
- Cena de bootstrap mínima.
- Primeiro build Web vazio abre em navegador.

### 2.2 Marco M1 — Partida mínima funcional

- Percurso numerado gerado de dados.
- Peças provisórias para 2–4 participantes.
- Sorteio inicial do número da sorte.
- Sorteio do número do azar por rodada.
- Dois resultados de dados e escolha de um.
- Movimento, resolução de risco, vitória, eliminação e reinício.
- UI simples com posição, jogador atual e números ativos.
- Hot-seat e bot simples (evita azar, aproxima da sorte) usando a mesma API de comandos.
- Limite de 15 rodadas com vitória do mais próximo e vitória compartilhada em empate.
- Testes unitários das regras e teste manual no navegador.

### 2.3 Marco M2 — Estratégia e apresentação

- Quatro cartas iniciais: `Reroll`, `Swap`, `MoveBackThree`, `Shield` (**propostas**).
- Aquisição, uso, descarte e limite de mão, após aprovação das regras.
- Animações leves, áudio e feedback de eventos.
- Bots: um bot simples foi antecipado para o M1 (decisão de 07/11/2026); no M2, ampliar a política do bot para cartas.

### 2.4 Marco M3 — Vertical slice artístico

- Um cenário 2.5D modular e legível.
- Personagens 2D como recortes de papel.
- HUD de cartas impressas.
- Ajustes de iluminação, memória e tempo de carregamento.
- Build Web final testado em navegadores-alvo.

**Fora do MVP:** seis cidades, habilidades únicas de personagens, campanha, progressão, monetização, multiplayer online obrigatório e eventos complexos.

---

## 3. Decisões de design pendentes

**O Claude Code não deve escolher silenciosamente estas regras.** Para permitir prototipagem, a tabela apresenta defaults *temporários* que exigem confirmação antes de se tornarem definitivos.

> **Atualização 07/11/2026:** linhas marcadas **Decidido (M1)** foram aprovadas pelo usuário para o protótipo. Continuam configuráveis em `MatchRulesConfig` e podem mudar após playtest.

| Tema | Default de protótipo | Status |
|---|---|---|
| Estrutura | Turnos sequenciais | Proposto |
| Jogadores | 2–4 | Proposto |
| Casas | Índices `0..30` fixos, sendo `0` início e `1..30` numeradas; independe do número da sorte. Casas `> LuckyNumber` não são alcançáveis por movimento normal | **Decidido (M1)** |
| Número da sorte | Sorteado em `15..30` no início | Proposto |
| Número do azar | Sorteado em `1..LuckyNumber−1` no início de cada rodada (`NextInt(1, LuckyNumber)`); revelado antes de qualquer jogada | **Decidido (M1)** |
| Rodada | Concluída quando todos os jogadores ativos tiveram oportunidade de agir; ordem fixa por `Player.Id`; eliminação no meio da rodada não ressorteia o azar | **Decidido (M1)** |
| Dois dados | Dois valores `1..6`; jogador escolhe **um**; escolha obrigatória mesmo se ambos levam ao azar | **Decidido (M1)** |
| Exceder o alvo | **Rebote no número da sorte** (não no limite do tabuleiro) | **Decidido (M1)** |
| Vitória | Terminar o movimento exatamente na casa da sorte | Proposto |
| Azar | Eliminação ao **terminar** movimento na casa do azar | Proposto |
| Sorte e azar coincidentes | Impossível pela faixa do azar (`< LuckyNumber`) | **Decidido (M1)** |
| Já estar na casa do azar quando ela é sorteada | Sem eliminação retroativa | Proposto |
| Último sobrevivente | Vence se os demais foram eliminados | Proposto |
| Cartas | Uma carta ativa por turno; defesa reativa sob regra específica | Pendente |
| Casas ocupadas | Permitir múltiplas peças na mesma casa, com deslocamento visual | Proposto |
| Limite de rodadas | `MaxRounds = 15` | **Decidido (M1)** |
| Fim por limite | Vence o jogador ativo de maior posição (mais próximo da sorte); empatados na mesma casa **compartilham a vitória** | **Decidido (M1)** |
| Modo local | Hot-seat + bot simples já no M1 | **Decidido (M1)** |
| Azar atrás de todos os jogadores | Permitido no M1; avaliar em playtest | Proposto |
| Troca de posição e risco | Ainda não definido | Em aberto |
| Movimento para trás além da casa inicial | Ainda não definido | Em aberto |

**Ponto crítico:** como a vitória exige chegar exatamente à casa da sorte, o algoritmo de rebote considera o **destino final**, não casas atravessadas. Ver §7.2.

---

## 4. Arquitetura em camadas

```text
┌─────────────────────────────────────────────────────┐
│ Presentation (Unity)                                │
│ Scenes, HUD, Input, Camera, Animation, Audio, VFX    │
└──────────────────────────┬──────────────────────────┘
                           │ comandos / eventos
┌──────────────────────────▼──────────────────────────┐
│ Application                                         │
│ MatchController, CommandDispatcher, StatePresenter  │
└──────────────────────────┬──────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────┐
│ Domain (C# puro)                                    │
│ MatchState, RulesEngine, TurnResolver, DiceService   │
│ CardEffects, BoardRules, Win/Loss validation         │
└──────────────────────────┬──────────────────────────┘
                           │ abstrações
┌──────────────────────────▼──────────────────────────┐
│ Infrastructure                                      │
│ Random provider, local input, save/config, future   │
│ network adapter                                    │
└─────────────────────────────────────────────────────┘
```

### 4.1 Domain — regras puras

Sem referências a `UnityEngine`, `GameObject`, `Transform`, `Time`, `Random` da Unity ou APIs de rede. Recebe comandos e estado, devolve resultado validado e eventos de domínio.

Responsabilidades:
- Sorteios via interface injetada.
- Validação de ações.
- Controle de turno/rodada.
- Movimentação e destino final.
- Resolução de número da sorte/azar.
- Aplicação de efeitos de cartas.
- Condições de encerramento.

### 4.2 Application — orquestração

Coordena as ações vindas da interface e chama o motor de regras. Publica eventos para apresentação, sem modificar diretamente as regras.

### 4.3 Presentation — Unity

Mostra o estado e toca animações. A posição do `Transform` **nunca** é a fonte da verdade sobre a casa de um jogador.

### 4.4 Infrastructure — serviços substituíveis

Implementa geração aleatória, persistência opcional e, futuramente, transporte de rede. O MVP usa um adaptador local.

---

## 5. Modelo de domínio

### 5.1 Tipos principais

```csharp
public enum MatchPhase
{
    NotStarted,
    RoundStart,
    AwaitingRoll,
    AwaitingDiceChoice,
    ResolvingAction,
    TurnEnd,
    Finished
}

public enum PlayerStatus { Active, Eliminated, Winner }

public sealed class PlayerState
{
    public int Id { get; }
    public int Position { get; internal set; }
    public PlayerStatus Status { get; internal set; }
    public List<string> CardIds { get; } = new();
}

public sealed class MatchState
{
    public MatchPhase Phase { get; internal set; }
    public int RoundNumber { get; internal set; }
    public int ActivePlayerId { get; internal set; }
    public int LuckyNumber { get; internal set; }
    public int UnluckyNumber { get; internal set; }
    public IReadOnlyList<PlayerState> Players { get; }
}
```

**Observação:** estes trechos são **contratos ilustrativos**, não código pronto para copiar sem adaptações. A implementação deve proteger invariantes, evitar mutações externas e expor snapshots somente leitura quando necessário.

### 5.2 Invariantes

- `Player.Id` é único na partida.
- `Position` está sempre entre `0` e `LuckyNumber` (≤ `BoardLength`), consequência do rebote no número da sorte.
- Jogador eliminado não recebe turno.
- Números de sorte e azar são distintos.
- Uma partida finalizada não aceita comandos de gameplay.
- Cada ação só pode ser executada na fase apropriada.
- O mesmo comando não pode ser aplicado duas vezes por acidente.
- Sorteios utilizam o provedor configurado, sem chamadas aleatórias escondidas na UI.

### 5.3 Configuração de regras

Criar `MatchRulesConfig` em C# puro com:
- `BoardLength`
- `MinLuckyNumber`
- `MaxLuckyNumber`
- `DiceCount`
- `DiceSides`
- `StartingHandSize`
- `MaxHandSize`
- `MaxRounds` (aprovado: `15` no M1)
- política de ultrapassagem
- política de eliminação
- política de empate

No Unity, um `ScriptableObject` (`MatchConfigAsset`) poderá serializar esses valores e convertê-los para `MatchRulesConfig` no início da partida. **Não armazenar estado mutável de partida no ScriptableObject.**

---

## 6. Fluxo de estados e comandos

### 6.1 Máquina de estados

```text
NotStarted
    │ StartMatch
    ▼
RoundStart ── DrawUnluckyNumber ──► AwaitingRoll
                                       │ RollDice
                                       ▼
                                AwaitingDiceChoice
                                       │ ChooseDie
                                       ▼
                                 ResolvingAction
                                       │
                         ┌─────────────┴─────────────┐
                         ▼                           ▼
                      TurnEnd                      Finished
                         │
              ┌──────────┴───────────┐
              ▼                      ▼
         AwaitingRoll            RoundStart
          (próximo)            (nova rodada)
```

O fluxo de cartas poderá adicionar subfases específicas; **não** incluir estados hipotéticos antes de definir timing e prioridades.

### 6.2 Comandos

| Comando | Dados | Validação |
|---|---|---|
| `StartMatch` | jogadores, config, seed | quantidade válida, fase inicial |
| `RollDice` | `playerId` | turno correto, fase `AwaitingRoll` |
| `ChooseDie` | `playerId`, `dieIndex` | índice válido, rolagem pendente |
| `PlayCard` | `playerId`, `cardId`, `targetId?` | posse, fase, alvo, custo |
| `RestartMatch` | nova seed / configuração | permitido pelo controlador |

A aplicação deve produzir um `CommandResult` com `Success`, `ErrorCode`, `StateSnapshot` e lista ordenada de eventos.

### 6.3 Eventos

- `MatchStarted`
- `RoundStarted`
- `UnluckyNumberDrawn`
- `DiceRolled`
- `DieSelected`
- `PlayerMoved`
- `CardPlayed`
- `PlayerEliminated`
- `PlayerWon`
- `TurnChanged`
- `MatchFinished`

Eventos devem conter os dados necessários à apresentação, por exemplo posição inicial/final e razão da eliminação. Evitar usar textos de UI como contrato de domínio.

---

## 7. Tabuleiro e movimentação

### 7.1 Representação lógica

Tabuleiro inicial **linear**, com casas de `0` a `N`. `0` representa a largada. Não criar caminhos ramificados no MVP.

```csharp
public readonly struct BoardTile
{
    public int Index { get; }
    public BoardTile(int index) => Index = index;
}
```

As posições físicas das casas ficam no Unity, em `BoardView`, associadas ao índice lógico. O cenário poderá curvar o percurso sem mudar a ordem lógica.

### 7.2 Rebote — algoritmo (Decidido para M1, 07/11/2026)

O rebote ocorre no **número da sorte** (`L = LuckyNumber`), não no limite físico `N` do tabuleiro. Para um movimento positivo:
1. Partir de `position` (sempre `0..L`).
2. Avançar uma casa lógica por passo.
3. Ao atingir `L` com passos restantes, inverter o sentido dos passos restantes.
4. O destino final determina sorte/azar; passar por `L` não vence.

**Exemplos:**
- `L=20`, posição `18`, dado `4` → `19 → 20 → 19 → 18`; destino `18`.
- `L=20`, posição `18`, dado `2` → `19 → 20`; destino `20`, vitória.
- `L=30`, posição `28`, dado `5` → `29 → 30 → 29 → 28 → 27`; destino `27`.

Consequência: posições ficam sempre em `0..L`; casas `L+1..N` existem só na apresentação. Para o MVP, não aceitar movimento maior do que `2*L` sem definir comportamento cíclico; com D6 e `L ≥ 15` isso não ocorre.

### 7.3 Mapeamento visual

`BoardView`:
- mantém `TileView[]` ordenado;
- expõe `GetTileAnchor(index)`;
- posiciona peças com pequenos offsets se múltiplas ocuparem a mesma casa;
- aplica realces para sorte e azar;
- anima movimento **após** a resolução da regra.

`BoardLayoutAsset` poderá conter referências ou posições das casas, permitindo criar outras cidades sem reescrever o domínio.

---

## 8. Dados e aleatoriedade

Interface proposta:

```csharp
public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
}
```

Requisitos:
- Uma seed por partida para reproduzir cenários.
- `RollDice` produz dois resultados `1..6`.
- O resultado fica registrado no estado enquanto o jogador escolhe.
- Não rerrolar ao redesenhar a UI.
- Testes utilizam `FakeRandomSource` com sequência conhecida.
- A ordem dos sorteios deve ser documentada para preservar reprodutibilidade.

**Segurança:** no multiplayer online futuro, o cliente não deve ser autoridade sobre resultados aleatórios. O MVP local não precisa de mecanismo antitrapaça.

---

## 9. Sorte, azar e encerramento

### 9.1 Sorteio inicial

`LuckyNumber` é escolhido uma única vez no início da partida, dentro do intervalo configurado.

### 9.2 Sorteio por rodada

`UnluckyNumber` é escolhido no início da rodada em `1..LuckyNumber−1` (decidido para M1), portanto nunca coincide com `LuckyNumber` nem com a largada. A apresentação revela o número antes de aceitar movimentos. Eliminações no meio da rodada não disparam novo sorteio.

### 9.2.1 Fim por limite de rodadas (Decidido para M1)

Ao concluir a rodada `MaxRounds` (15) sem vencedor, a partida termina: vencem todos os jogadores ativos com a maior posição. O resultado deve suportar **múltiplos vencedores** e informar o motivo de término (`ExactLucky`, `LastSurvivor`, `RoundLimit`).

### 9.3 Resolução de destino — ordem proposta

1. Validar movimento e calcular destino.
2. Atualizar posição lógica.
3. Verificar se o destino é a casa da sorte.
4. Caso contrário, verificar se é a casa do azar.
5. Aplicar proteção defensiva, se houver regra aprovada.
6. Verificar condição de último sobrevivente.
7. Avançar turno ou finalizar.

A ordem exata para efeitos de cartas, escudos e trocas de posição **permanece pendente** e deverá ser formalizada antes do Marco M2.

### 9.4 Casos especiais

- Se o azar for sorteado na casa já ocupada, não eliminar automaticamente.
- Jogadores eliminados são ignorados no escalonamento de turnos.
- Quando a partida termina, cancelar entradas pendentes e bloquear ações.
- Reinício deve criar novo estado, limpar dados pendentes, animações e listeners temporários.
- Se todos os jogadores forem eliminados por efeitos simultâneos futuros, exigir regra explícita de empate.

---

## 10. Cartas — arquitetura extensível

### 10.1 Dados e efeitos

`CardDefinition` (`ScriptableObject`) guarda:
- `Id` estável
- título
- descrição
- arte
- tipo de alvo
- parâmetros configuráveis

`ICardEffect` / `CardEffectResolver` interpreta o identificador do efeito no domínio, valida alvo e aplica mudanças ao estado. **Não** colocar a regra de negócio dentro de métodos `OnClick()` nem executar a lógica diretamente no `ScriptableObject`.

### 10.2 Quatro cartas candidatas

| ID sugerido | Efeito proposto | Pendência |
|---|---|---|
| `reroll` | Nova rolagem | momento de uso |
| `swap` | Troca de posições | risco e prioridade |
| `move_back_3` | Recuar três casas | limite inferior e risco |
| `shield` | Evitar uma eliminação | duração e consumo |

Antes de implementar, documentar: fase permitida, alvo, resultado, interação com sorte/azar, cancelamento e desempate.

---

## 11. Jogadores, bots e multiplayer

### 11.1 Jogadores locais

`PlayerController` na apresentação recebe input e envia comandos identificados pelo `playerId`. Para turnos sequenciais, uma mesma interface pode controlar todos os jogadores, com indicação clara do turno.

### 11.2 Bots

Bots devem usar a **mesma API de comandos** que humanos. Um `BotDecisionService` recebe snapshot e lista de ações válidas e escolhe uma. Primeira versão: política simples de evitar azar e buscar a casa da sorte.

### 11.3 Preparação para multiplayer online

Separar:
- **comandos** (intenção do jogador);
- **estado autoritativo** (resolução);
- **eventos/snapshots** (sincronização e apresentação).

Nenhuma biblioteca de networking será adicionada ao MVP sem escolha explícita e verificação de compatibilidade Web. Em navegadores, verificar suporte do transporte escolhido (por exemplo, WebSockets/serviço compatível) e limitações de hospedagem. **itch.io hospeda o cliente Web, não substitui automaticamente um servidor de partidas.**

---

## 12. Estrutura recomendada no Unity

```text
DontTrustYourLuck/
├── Assets/
│   └── _Game/
│       ├── Art/
│       │   ├── Characters/
│       │   ├── Environment/
│       │   ├── Materials/
│       │   ├── Textures/
│       │   └── UI/
│       ├── Audio/
│       ├── Prefabs/
│       │   ├── Board/
│       │   ├── Characters/
│       │   └── UI/
│       ├── Scenes/
│       │   ├── Bootstrap.unity
│       │   ├── MainMenu.unity
│       │   └── GameBoard.unity
│       ├── Scripts/
│       │   ├── Domain/
│       │   ├── Application/
│       │   ├── Infrastructure/
│       │   ├── Presentation/
│       │   │   ├── Board/
│       │   │   ├── Characters/
│       │   │   ├── UI/
│       │   │   └── Audio/
│       │   └── Editor/
│       ├── Data/
│       │   ├── Cards/
│       │   ├── Boards/
│       │   └── Characters/
│       └── Tests/
│           ├── EditMode/
│           └── PlayMode/
├── Packages/
├── ProjectSettings/
├── docs/
│   ├── GDD.md
│   └── TECHNICAL_DESIGN.md
├── CLAUDE.md
├── README.md
└── .gitignore
```

A estrutura é **alvo**, não instrução para criar dezenas de diretórios vazios. Criar cada pasta conforme necessário.

### 12.1 Assemblies (opcional após a primeira implementação)

- `DontTrustYourLuck.Domain`: C# puro, sem dependência de Unity.
- `DontTrustYourLuck.Runtime`: integração Unity; referencia Domain.
- `DontTrustYourLuck.Tests.EditMode`: testes do domínio.
- `DontTrustYourLuck.Tests.PlayMode`: testes de integração.

Não introduzir `asmdef` desnecessários antes de avaliar o custo-benefício no projeto real.

---

## 13. Cenas e inicialização

### `Bootstrap.unity`
- Configuração de serviços e carregamento da cena de jogo.
- Evitar singletons globais espalhados.

### `MainMenu.unity`
- Iniciar partida, instruções, configurações básicas.
- No M1, pode ser substituído por botão simples de início.

### `GameBoard.unity`
- `BoardView`
- `CharacterViews`
- `GameHUD`
- `MatchController`
- Câmera
- Iluminação
- Ambiente provisório

**Inicialização sugerida:** carregar configuração → instanciar domínio → registrar apresentadores → iniciar partida → emitir snapshot inicial.

---

## 14. UI, câmera e input

### 14.1 HUD mínimo

- Número da sorte e número do azar.
- Jogador atual e rodada.
- Posições dos participantes.
- Dois resultados dos dados e botões de escolha.
- Mensagens de ação inválida.
- Vitória/eliminação e botão de reinício.

### 14.2 Responsividade

- `Canvas Scaler`: **Scale With Screen Size**.
- Resolução de referência: `1280×720`.
- Âncoras corretas para bordas e cantos.
- Testar em janelas menores sem cortar botões.
- Não depender de hover para ações essenciais.
- Manter o tabuleiro legível mesmo com HUD ornamentada.

### 14.3 Câmera

- Câmera elevada fixa ou com movimento limitado.
- Preferir enquadramento completo do tabuleiro no MVP.
- Se usar perspectiva, evitar que peças ocultem números.
- Personagens 2D voltados à câmera, com teste de ordenação e sombras.

### 14.4 Input

O Input System moderno é recomendado, **mas verificar o pacote existente antes de instalar ou substituir**. Não misturar sistemas de input sem necessidade.

---

## 15. Pipeline artístico 2.5D

### 15.1 Ambiente

- Malhas 3D modulares, texturas de baixa resolução e paleta controlada.
- Geometria simples com silhuetas fortes.
- Atlas de texturas quando útil.
- Materiais compartilhados para reduzir mudanças de estado.
- Vegetação e decoração posicionadas sem prejudicar leitura das casas.

### 15.2 Personagens

- Sprites 2D em planos no mundo 3D ou `SpriteRenderer` conforme protótipo visual.
- Testar sorting, oclusão, billboard e sombra no URP real.
- Evitar transparências grandes e sobrepostas.

### 15.3 UI

- Cartas envelhecidas e ornamentação latino-americana.
- Sorte: destaque dourado/verde; azar: vermelho/roxo.
- Contraste e hierarquia visual acima da ornamentação.

### 15.4 Referências culturais

Primeiro cenário proposto: Fortaleza, com referências gerais à linguagem gráfica de Aldemir Martins. Criar assets **originais**; não copiar obras protegidas nem insinuar parceria oficial.

---

## 16. Restrições específicas do WebGL

### 16.1 Compatibilidade

- Não depender de plugins nativos desktop.
- Não presumir acesso livre ao sistema de arquivos.
- Evitar bibliotecas que exijam sockets TCP/UDP diretos no navegador.
- Não exigir multithreading convencional para lógica central.
- Verificar cada pacote, shader e API em build Web real.
- Considerar políticas do navegador para inicialização de áudio e fullscreen.
- Evitar operações síncronas pesadas que bloqueiem o loop principal.

### 16.2 Memória e performance

- Controlar tamanho de texturas, áudio e bundles.
- Evitar alocações recorrentes em `Update`.
- Preferir iluminação estática e poucas luzes em tempo real.
- Minimizar transparências, overdraw e partículas.
- Medir draw calls, tempo de frame e memória em dispositivo/navegador real.
- Não prometer orçamento de memória sem perfil de hardware e medição.

### 16.3 Build e publicação

1. Confirmar módulo **Web Build Support**.
2. Selecionar perfil **Web** em Build Profiles.
3. Compilar um build de desenvolvimento e executar em servidor HTTP local, não por abertura direta de `index.html` via `file://`.
4. Inspecionar console do navegador.
5. Testar carregamento, input, partida completa e reinício.
6. Preparar build de distribuição.
7. Compactar os arquivos gerados para upload HTML5 no itch.io.
8. Conferir compressão e headers do host; fallback de descompressão somente se necessário e validado.
9. Testar página publicada, não apenas servidor local.

**Não assumir que Brotli funciona corretamente sem configuração de hospedagem e teste.**

---

## 17. Testes e critérios de aceitação

### 17.1 EditMode — domínio

| Caso | Resultado esperado |
|---|---|
| Sorte e azar distintos | Nunca coincidem |
| Dados | Cada valor entre 1 e 6 |
| Escolha inválida | Comando rejeitado sem alterar estado |
| Movimento normal | Destino calculado corretamente |
| Rebote | Destino final segue regra configurada |
| Passar pela sorte sem parar | Não vence |
| Parar na sorte | Vence |
| Parar no azar | Elimina conforme política |
| Estar na casa quando azar muda | Sem eliminação retroativa |
| Jogador eliminado | Não recebe turno |
| Último ativo | Encerra partida conforme regra |
| Reiniciar | Limpa estado anterior |
| Seed fixa | Resultados reproduzíveis |
| Carta inválida | Não altera estado |

### 17.2 PlayMode — integração

- Peças correspondem às posições lógicas.
- Botões só ficam ativos nas fases permitidas.
- HUD acompanha sorteio e mudança de rodada.
- Vitória bloqueia comandos subsequentes.
- Reinício restaura a cena/estado sem duplicar listeners.
- Vários jogadores na mesma casa permanecem visíveis.

### 17.3 Web — smoke test obrigatório

- Abre no navegador sem erro fatal.
- Inputs funcionam.
- Uma partida inteira pode ser concluída.
- Animações não impedem regras.
- Reinício funciona.
- Sem erros críticos no console.
- Layout permanece utilizável em pelo menos dois tamanhos de janela.
- Medições de carregamento e FPS são registradas, não estimadas.

### 17.4 Definition of Done

Uma funcionalidade só está concluída quando:
1. O comportamento foi implementado.
2. Critérios de aceitação foram verificados.
3. Testes executados e não executados foram discriminados.
4. Não houve regressão conhecida sem registro.
5. Compatibilidade Web foi verificada no marco correspondente.
6. Mudanças e limitações foram resumidas.

---

## 18. Convenções para Claude Code

- Ler `CLAUDE.md`, `docs/GDD.md` e este documento antes de alterações arquiteturais.
- Inspecionar arquivos e pacotes existentes antes de criar novos.
- Explicar a mudança proposta e os critérios de aceitação.
- Implementar uma funcionalidade coesa por vez.
- Não alterar `.meta`/GUIDs manualmente sem justificativa.
- Não reescrever cenas YAML complexas sem verificar integridade.
- Não instalar pacotes ou criar commits sem autorização.
- Não alegar testes no Editor/Web quando não foram executados.
- Manter `Assets/`, `Packages/` e `ProjectSettings/` versionados; ignorar pastas geradas.
- Responder em português brasileiro; código e identificadores podem ficar em inglês.
- Se uma tarefa depender de regra pendente, solicitar decisão antes de implementar comportamento irreversível.

---

## 19. Primeiro plano de implementação

### Tarefa 0 — Diagnóstico sem alterações

**Entrada:** repositório Unity recém-criado.  
**Saída:** relatório de versão, pacotes, URP, perfil Web, estrutura e problemas.  
**Aceite:** nenhum arquivo modificado.

### Tarefa 1 — Fundação

- Preparar cena mínima e bootstrap.
- Criar configuração de partida.
- Criar projeto de testes do domínio.
- Gerar build Web vazio.

**Aceite:** projeto abre, testes-base passam e página Web carrega.

### Tarefa 2 — Domínio do tabuleiro

- `MatchState`, `PlayerState`, `MatchRulesConfig`.
- Regras de movimento e rebote.
- Testes de limites.

**Aceite:** lógica executa em EditMode sem cena.

### Tarefa 3 — Dados e sorte

- RNG injetável.
- Sorteio de lucky/unlucky.
- Comandos de rolagem e escolha.
- Testes de seed e validação.

### Tarefa 4 — Partida completa

- Turnos/rodadas.
- Vitória e eliminação.
- UI e peças provisórias.
- Reinício e smoke test Web.

### Tarefa 5 — Cartas

Somente após resolver as regras pendentes de timing, alvos e efeitos.

### Tarefa 6 — Direção artística

Trocar placeholders por ambiente e personagens 2.5D, mantendo contratos do domínio.

---

## 20. Registro de decisões técnicas (ADR)

Manter decisões relevantes em `docs/decisions/` **somente quando surgirem**. Cada ADR deve conter: contexto, opções, decisão, consequências e data.

**ADRs iniciais sugeridos:**
- `ADR-001`: Unity Web como plataforma principal.
- `ADR-002`: regras em C# puro, separadas da Unity.
- `ADR-003`: modo local primeiro, rede como adaptação futura.
- `ADR-004`: regras de movimentação e rebote (decidido 07/11/2026: rebote no número da sorte, tabuleiro físico `0..30`).
- `ADR-005`: política de cartas e eliminação (pendente).

---

## 21. Checklist antes de codificar

- [ ] Confirmar versão e pacotes reais do Unity.
- [ ] Confirmar URP e suporte Web.
- [ ] Colocar GDD e TDD em `docs/`.
- [ ] Revisar regras pendentes da seção 3.
- [ ] Validar `.gitignore` para Unity.
- [ ] Fazer build Web mínimo.
- [ ] Iniciar domínio e testes.
- [ ] Construir vertical slice com placeholders.
- [ ] Somente então investir em assets finais.

**Regra central:** o projeto deve ser **jogável e testável antes de ser visualmente completo**.
