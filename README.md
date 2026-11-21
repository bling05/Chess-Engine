# Chess Engine in C#

A chess engine I built on top of [Sebastian Lague's Chess Challenge](https://github.com/SebLague/Chess-Challenge) framework. The framework provides the board, legal move generation, and the UI. Everything that decides which move to play is mine, in [`src/My Bot`](Chess-Challenge-main/Chess-Challenge/src/My%20Bot):

- [`MyBot.cs`](Chess-Challenge-main/Chess-Challenge/src/My%20Bot/MyBot.cs): search and evaluation
- [`PieceSquareTables.cs`](Chess-Challenge-main/Chess-Challenge/src/My%20Bot/PieceSquareTables.cs): positional tables
- [`PolyglotBook.cs`](Chess-Challenge-main/Chess-Challenge/src/My%20Bot/PolyglotBook.cs): opening book reader and Zobrist hashing

It plays roughly even with me, and I'm rated about 1700 on Lichess.

## How it works

The engine started as a template that picked a random legal move. Each step below fixed the biggest weakness of the step before it.

### 1. Looking ahead: minimax

Chess is a turn-based decision game, so every position branches into a tree of possible futures. Minimax searches that tree recursively: on its own turn the engine picks the move with the best score, and it assumes the opponent will always pick the move that's worst for the engine. This was the first point where it found real tactical sequences.

### 2. Searching deeper: alpha-beta pruning

Plain minimax is slow. A chess position has about 35 legal moves on average, so the tree grows as 35^depth: about 52 million positions at 5 ply and 1.8 billion at 6.

Alpha-beta pruning skips branches that can't change the final decision. Once one reply shows a move is worse than an option already found, the rest of that move's replies don't need to be searched. With good move ordering this cuts the work from O(b^d) to about O(b^(d/2)), roughly doubling the depth reachable in the same time. Getting the alpha and beta bounds right took a while: an early version misapplied them and pruned away the winning moves.

### 3. Judging positions: material plus piece-square tables

Counting material alone treats a knight on the rim the same as a knight in the center. Piece-square tables give each piece type a bonus or penalty for every square, so a piece's value depends on where it stands. The king uses two tables, one that keeps it sheltered in the middlegame and one that brings it toward the center in the endgame, and the engine switches between them based on the material left on the board. The starting values come from the well-known [Simplified Evaluation Function](https://www.chessprogramming.org/Simplified_Evaluation_Function).

One bug here: my tables were indexed in the opposite direction from how the board reads FEN strings, so pieces were being scored for the wrong squares.

### 4. Playing openings well: Polyglot opening books

Search alone plays openings poorly, so the engine reads Polyglot opening books: binary files of positions and moves already analyzed by strong engines and players.

Each book entry is 16 bytes, stored big-endian and sorted by key:

| Bytes | Field | Meaning |
|---|---|---|
| 0–7 | key (UInt64) | Zobrist hash of the position |
| 8–9 | move (UInt16) | from square, to square, and promotion, packed into bits |
| 10 | weight | how popular the move is |
| 11 | learn | used by learning engines (ignored here) |
| 12–15 | unused | padding |

To find a book move, the engine:

1. Converts the board to a FEN string.
2. Computes the position's 64-bit Zobrist hash by XORing together keys from Polyglot's fixed table of 781 random numbers: 768 for each piece on each square (12 piece types × 64 squares), 4 for castling rights, 8 for en passant files, and 1 for white to move. The output matches the reference hashes in the Polyglot spec (for example `463b96181691fc9c` for the starting position).
3. Binary-searches the sorted file on disk for that hash, seeking to entries instead of loading the whole book into memory, then collects every move stored for the position and picks one at random for variety.
4. Decodes the packed 16-bit move with bit masks and shifts and converts it to UCI notation (like `e2e4`).

This was the hardest part to build. Documentation for Polyglot hashing was hard to find, and the full key table turned up only on an old, obscure site.

## Running it

You need the [.NET 6 SDK](https://dotnet.microsoft.com/download) or newer.

```
cd Chess-Challenge-main/Chess-Challenge
dotnet run
```

Then choose a match against the engine from the menu.

### Opening books

- `Human.bin` (in the repo) holds unusual, human-style openings, for fun.
- `Cerebellum3Merge.bin` is the engine's main book, built from strong engine play (Cerebellum, from the Brainfish project). It's too large for GitHub, so download it separately and put it in `Chess-Challenge-main/Chess-Challenge/` next to `Human.bin`.

The engine uses `Cerebellum3Merge.bin` when it's there and `Human.bin` otherwise, and once a game leaves the book it goes back to searching.

## Next steps

- **Quiescence search:** keep searching captures past the depth limit, so the engine doesn't stop just before a losing exchange (the horizon effect).
- **Transposition table:** cache results for positions already searched, keyed by the Zobrist hash the engine already computes.
- **Killer move heuristic:** try moves that caused cutoffs at the same depth first, so alpha-beta prunes more.
- **Passed pawn detection:** use bitboard masks to spot pawns that can't be stopped by enemy pawns, which decide many endgames.
- **Check extensions:** search one ply deeper when in check, to find longer forced mates.
