using System;
using ChessChallenge.API;
using System.Collections.Generic;

public class MyBot : IChessBot {

    public Move Think(Board board, Timer timer) {

        var book = new PolyglotBook();
        ulong zobristKey = book.ComputeZobristKeyFromFen(board.GetFenString());

        string? move = book.FindBookMove(zobristKey, "Cerebellum3Merge.bin");
        if (move != null) {
            Console.WriteLine("Book move: " + move);
            return new Move(move, board);
        }
        return MinimaxMove(board);

    }

    private Move MinimaxMove(Board board) {
        Move bestMove = Move.NullMove;
        int depth = 4;

        Move[] moves = board.GetLegalMoves();

        bool maximizingPlayer = board.IsWhiteToMove;
        int bestEval = maximizingPlayer ? int.MinValue : int.MaxValue;

        foreach (Move move in moves) {
            board.MakeMove(move);
            int eval = Minimax(board, depth - 1, int.MinValue, int.MaxValue, !maximizingPlayer);
            board.UndoMove(move);

            if (maximizingPlayer) {
                if (eval > bestEval) {
                    bestEval = eval;
                    bestMove = move;
                }
            } else {
                if (eval < bestEval) {
                    bestEval = eval;
                    bestMove = move;
                }
            }
        }

        return bestMove;
    }

    int Minimax(Board board, int depth, int alpha, int beta, bool maximizingPlayer) {
        if (board.IsInCheckmate()) {
            if (maximizingPlayer) {
                return int.MinValue + 1;
            }
            else {
                return int.MaxValue - 1;
            }
        }

        if (board.IsDraw()) {
            return 0;
        }

        if (depth == 0) {
            return Evaluate(board);
        }

        Move[] moves = board.GetLegalMoves();
        if (maximizingPlayer) {
            int maxEval = int.MinValue;
            foreach (Move move in moves) {

                // Check extensions to find deeper mate combinations
                // int newDepth = depth - 1;
                // if (board.IsInCheck()) {
                //     newDepth++;
                // }
                board.MakeMove(move);
                int eval = Minimax(board, depth - 1, alpha, beta, false);
                board.UndoMove(move);
                maxEval = Math.Max(maxEval, eval);
                alpha = Math.Max(alpha, eval);
                if (beta <= alpha) {
                    break;
                }
            }
            return maxEval;
        } else {
            int minEval = int.MaxValue;
            foreach (Move move in moves) {
                board.MakeMove(move);
                int eval = Minimax(board, depth - 1, alpha, beta, true);
                board.UndoMove(move);
                minEval = Math.Min(minEval, eval);
                beta = Math.Min(beta, eval);
                if (beta <= alpha) {
                    break;
                }
            }
            return minEval;
        }
    }

    int Evaluate(Board board) {
        // TODO:
        // - Blended king/pawn endgame PSTs
        // - Passed / isolated pawns using bitmasking
        // - Killer move heuristic
        // - Quiescence search
        // - Check extensions
        int score = 0;
        PieceType[] pieceTypes = { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen };
        int queenCount = 0;
        int nonQueenPieceCount = 0;

        // Material and piece position evaluation
        foreach (PieceType pieceType in pieceTypes) {
            PieceList whitePieces = board.GetPieceList(pieceType, true);
            foreach (Piece piece in whitePieces) {
                score += GetPieceMaterialValue(pieceType);
                score += GetPiecePositionValue(pieceType, piece.Square, true);
                if (pieceType == PieceType.Queen) {
                    queenCount++;
                }
                else {
                    nonQueenPieceCount++;
                }
            }

            PieceList blackPieces = board.GetPieceList(pieceType, false);
            foreach (Piece piece in blackPieces) {
                score -= GetPieceMaterialValue(pieceType);
                score -= GetPiecePositionValue(pieceType, piece.Square, false);
                if (pieceType == PieceType.Queen) {
                    queenCount++;
                }
                else if (pieceType != PieceType.Pawn) {
                    nonQueenPieceCount++;
                }
            }
        }

        // King safety evaluation
        int whiteKingIndex = MirrorSquare(board.GetKingSquare(true).Index);
        int blackKingIndex = board.GetKingSquare(false).Index;
        if ((queenCount == 0 && nonQueenPieceCount <= 4) || nonQueenPieceCount <= 1) {
            score += PieceSquareTables.KingPSTendgame[whiteKingIndex];
            score -= PieceSquareTables.KingPSTendgame[blackKingIndex];
        }
        else {
            score += PieceSquareTables.KingPST[whiteKingIndex];
            score -= PieceSquareTables.KingPST[blackKingIndex];
        }

        return score;
    }

    int GetPieceMaterialValue(PieceType pieceType) {
        // 1. Minor piece > 3 pawns
        // 2. Bishop pair > 2 other minor pieces
        // 3. 2 Minor pieces > Rook and pawn
        switch(pieceType) {
            case PieceType.Pawn: return 100;
            case PieceType.Knight: return 320;
            case PieceType.Bishop: return 330;
            case PieceType.Rook: return 500;
            case PieceType.Queen: return 900;
            default: return 0;
        }
    }

    int GetPiecePositionValue(PieceType pieceType, Square square, bool isWhite) {
        int squareIndex = square.Index;

        switch(pieceType) {
            case PieceType.Pawn:
                if (isWhite) {
                    squareIndex = MirrorSquare(squareIndex);
                }
                return PieceSquareTables.PawnPST[squareIndex];
            case PieceType.Knight:
                if (isWhite) {
                    squareIndex = MirrorSquare(squareIndex);
                }
                return PieceSquareTables.KnightPST[squareIndex];
            case PieceType.Bishop:
                if (isWhite) {
                    squareIndex = MirrorSquare(squareIndex);
                }
                return PieceSquareTables.BishopPST[squareIndex];
            case PieceType.Rook:
                if (isWhite) {
                    squareIndex = MirrorSquare(squareIndex);
                }
                return PieceSquareTables.RookPST[squareIndex];
            case PieceType.Queen:
                if (isWhite) {
                    squareIndex = MirrorSquare(squareIndex);
                }
                return PieceSquareTables.QueenPST[squareIndex];
            default:
                return 0;
        }
    }

    int MirrorSquare(int squareIndex) {
        // Flip the square index vertically to mirror the board for black PST tables
        int rank = squareIndex / 8;
        int file = squareIndex % 8;
        int mirroredRank = 7 - rank;
        return mirroredRank * 8 + file;
    }

}