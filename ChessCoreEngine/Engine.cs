using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ChessEngine.Engine
{
    public sealed class Engine
    {
        #region InternalMembers

        internal List<OpeningMove> CurrentGameBook;
        internal List<OpeningMove> UndoGameBook;
        
        #endregion

        #region PrivateMembers

        private Board ChessBoard;
        private Board PreviousChessBoard;
        private Board UndoChessBoard;
        
        private Stack<MoveContent> MoveHistory;
        private List<OpeningMove> OpeningBook;

        private string pvLine;

        #endregion

        #region PublicMembers

        public enum Difficulty
        {
            Easy,
            Medium,
            Hard,
            VeryHard
        }

        public enum TimeSettings
        {
            Moves40In5Minutes,
            Moves40In10Minutes,
            Moves40In20Minutes,
            Moves40In30Minutes,
            Moves40In40Minutes,
            Moves40In60Minutes,
            Moves40In90Minutes,
        }

        public ChessPieceType PromoteToPieceType = ChessPieceType.Queen;

        public PiecesTaken PiecesTakenCount = new PiecesTaken();

        //State Variables
        public ChessPieceColor HumanPlayer;
        public bool Thinking;
        public bool TrainingMode;

        //Stats
        public int NodesSearched;
        public int NodesQuiessence;
        public byte PlyDepthSearched;
        public byte PlyDepthReached;
        public byte RootMovesSearched;

        public TimeSettings GameTimeSettings;

        public string FEN
        {
            get { return Board.Fen(false, ChessBoard); }
        }

        public MoveContent LastMove
        {
            get { return ChessBoard.LastMove; }
        }

        public Difficulty GameDifficulty
        {
           	get 
			{
				if (PlyDepthSearched == 3)
				{
					return Difficulty.Easy;
				}
				else if (PlyDepthSearched == 5)
				{
					return Difficulty.Medium;
				}
				else if (PlyDepthSearched == 6)
				{
					return Difficulty.Hard;
				}
				else if (PlyDepthSearched == 7)
				{
					return Difficulty.VeryHard;
				}

				return Difficulty.Medium;
			
			}
			set
            {
                if (value == Difficulty.Easy)
                {
                    PlyDepthSearched = 3;
                    GameTimeSettings = TimeSettings.Moves40In10Minutes;
                }
                else if (value == Difficulty.Medium)
                {
                    PlyDepthSearched = 5;
                    GameTimeSettings = TimeSettings.Moves40In20Minutes;
                }
                else if (value == Difficulty.Hard)
                {
                    PlyDepthSearched = 6;
                    GameTimeSettings = TimeSettings.Moves40In60Minutes;
                }
                else if (value == Difficulty.VeryHard)
                {
                    PlyDepthSearched = 7;
                    GameTimeSettings = TimeSettings.Moves40In90Minutes;
                }
            }
        }

        public ChessPieceColor WhoseMove
        {
            get { return ChessBoard.WhoseMove; }
            set { ChessBoard.WhoseMove = value; }
        }

        public bool StaleMate
        {
            get { return ChessBoard.StaleMate; }
            set { ChessBoard.StaleMate = value; }
        }
        public bool RepeatedMove
        {
            get
            {
                if (ChessBoard.RepeatedMove >= 3)
                {
                    return true;
                }

                return false;
            }
        }

        public string PvLine
        {
            get { return pvLine;}
        }

        public bool FiftyMove
        {
             get
             {
                 if (ChessBoard.FiftyMove >= 50)
                 {
                     return true;
                 }

                 return false;
             }
        }

        public bool InsufficientMaterial
        {
            get
            {
                return ChessBoard.InsufficientMaterial;
            }
        }

        #endregion

        #region Constructors

        public Engine()
        {
            InitiateEngine();
            InitiateBoard("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        }

        public Engine(string fen)
        {
            InitiateEngine();
            InitiateBoard(fen);
        }

        public void NewGame()
        {
            InitiateEngine();
            InitiateBoard("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        }

        public void InitiateBoard(string fen)
        {
            ChessBoard = new Board(fen);

            if (!String.IsNullOrEmpty(fen))
            {
                PieceValidMoves.GenerateValidMoves(ChessBoard);
                Evaluation.EvaluateBoardScore(ChessBoard);
            }
        }

        private void InitiateEngine()
        {
            GameDifficulty = Difficulty.Medium;

            MoveHistory = new Stack<MoveContent>();
            HumanPlayer = ChessPieceColor.White;
            OpeningBook = new List<OpeningMove>();
            CurrentGameBook = new List<OpeningMove>();
            PieceMoves.InitiateChessPieceMotion();
            LoadOpeningBook();
        }

        #endregion

        #region Methods

        public void SetChessPieceSelection(byte boardColumn, byte boardRow,
                                          bool selection)
        {
            byte index = GetBoardIndex(boardColumn, boardRow);

            if (ChessBoard.Squares[index].Piece == null)
            {
                return;
            }
            if (ChessBoard.Squares[index].Piece.PieceColor != HumanPlayer)
            {
                return;
            }
            if (ChessBoard.Squares[index].Piece.PieceColor != WhoseMove)
            {
                return;
            }
            ChessBoard.Squares[index].Piece.Selected = selection;
        }

        public int ValidateOpeningBook()
        {
            return Book.ValidateOpeningBook(OpeningBook);
        }

        private static bool CheckForMate(ChessPieceColor whosTurn, ref Board chessBoard)
        {
            Search.SearchForMate(whosTurn, chessBoard, ref chessBoard.BlackMate,
                                 ref chessBoard.WhiteMate, ref chessBoard.StaleMate);

            if (chessBoard.BlackMate || chessBoard.WhiteMate || chessBoard.StaleMate)
            {
                return true;
            }

            return false;
        }

        private static bool FindPlayBookMove(ref MoveContent bestMove, Board chessBoard, IEnumerable<OpeningMove> openingBook)
        {
            //Get the Hash for the current Board;
            string boardFen= Board.Fen(true, chessBoard);

            //Check the Opening Move Book
            foreach (OpeningMove move in openingBook)
            {
                if (move.StartingFEN.Contains(boardFen))
                {
                    int index = 0;

                    bestMove = move.Moves[index];
                    return true;
                }
            }

            return false;
        }

        public void Undo()
        {
            if (UndoChessBoard != null)
            {
                PieceTakenRemove(ChessBoard.LastMove);
                PieceTakenRemove(PreviousChessBoard.LastMove);

                ChessBoard = new Board(UndoChessBoard);
                CurrentGameBook = new List<OpeningMove>(UndoGameBook);

                PieceValidMoves.GenerateValidMoves(ChessBoard);
                Evaluation.EvaluateBoardScore(ChessBoard);
            }
        }

        private static byte GetBoardIndex(byte boardColumn, byte boardRow)
        {
            return (byte)(boardColumn + (boardRow * 8));
        }

        public byte[] GetEnPassantMoves()
        {
            if (ChessBoard == null)
            {
                return null;
            }

            var returnArray = new byte[2];

            returnArray[0] = (byte)(ChessBoard.EnPassantPosition % 8);
            returnArray[1] = (byte)(ChessBoard.EnPassantPosition / 8);

            return returnArray;
        }

        public bool GetBlackMate()
        {
            if (ChessBoard == null)
            {
                return false;
            }

            return ChessBoard.BlackMate;
        }

        public bool GetWhiteMate()
        {
            return ChessBoard.WhiteMate;
        }

        public bool GetBlackCheck()
        {
            return ChessBoard.BlackCheck;
        }

        public bool GetWhiteCheck()
        {
            return ChessBoard.WhiteCheck;
        }

        public byte GetRepeatedMove()
        {
            return ChessBoard.RepeatedMove;
        }

        public byte GetFiftyMoveCount()
        {
            return ChessBoard.FiftyMove;
        }

        public Stack<MoveContent> GetMoveHistory()
        {
            return MoveHistory;
        }

        public ChessPieceType GetPieceTypeAt(byte boardColumn, byte boardRow)
        {
            byte index = GetBoardIndex(boardColumn, boardRow);

            if (ChessBoard.Squares[index].Piece == null)
            {
                return ChessPieceType.None;
            }

            return ChessBoard.Squares[index].Piece.PieceType;
        }

        public ChessPieceType GetPieceTypeAt(byte index)
        {
            if (ChessBoard.Squares[index].Piece == null)
            {
                return ChessPieceType.None;
            }

            return ChessBoard.Squares[index].Piece.PieceType;
        }

        public ChessPieceColor GetPieceColorAt(byte boardColumn, byte boardRow)
        {
            byte index = GetBoardIndex(boardColumn, boardRow);

            if (ChessBoard.Squares[index].Piece == null)
            {
                return ChessPieceColor.White;
            }
            return ChessBoard.Squares[index].Piece.PieceColor;
        }

        public ChessPieceColor GetPieceColorAt(byte index)
        {
            if (ChessBoard.Squares[index].Piece == null)
            {
                return ChessPieceColor.White;
            }
            return ChessBoard.Squares[index].Piece.PieceColor;
        }

        public bool GetChessPieceSelected(byte boardColumn, byte boardRow)
        {
            byte index = GetBoardIndex(boardColumn, boardRow);

            if (ChessBoard.Squares[index].Piece == null)
            {
                return false;
            }

            return ChessBoard.Squares[index].Piece.Selected;
        }

        public void GenerateValidMoves()
        {
            PieceValidMoves.GenerateValidMoves(ChessBoard);
        }

        public int EvaluateBoardScore()
        {
            Evaluation.EvaluateBoardScore(ChessBoard);
            return ChessBoard.Score;
        }

        public byte[][] GetValidMoves(byte boardColumn, byte boardRow)
        {
            byte index = GetBoardIndex(boardColumn, boardRow);

            if (ChessBoard.Squares[index].Piece ==
                null)
            {
                return null;
            }

            var returnArray = new byte[ChessBoard.Squares[index].Piece.ValidMoves.Count][];
            int counter = 0;

            foreach (byte square in ChessBoard.Squares[index].Piece.ValidMoves)
            {
                returnArray[counter] = new byte[2];
                returnArray[counter][0] = (byte)(square % 8);
                returnArray[counter][1] = (byte)(square /8);
                counter++;
            }

            return returnArray;
        }

        public int GetScore()
        {
            return ChessBoard.Score;
        }

        public byte FindSourcePositon(ChessPieceType chessPieceType, ChessPieceColor chessPieceColor, byte dstPosition, bool capture, int forceCol, int forceRow)
        {
            Square square;

            if (dstPosition == ChessBoard.EnPassantPosition && chessPieceType == ChessPieceType.Pawn)
            {
                if (chessPieceColor == ChessPieceColor.White)
                {
                    square = ChessBoard.Squares[dstPosition + 7];

                    if (square.Piece != null)
                    {
                        if (square.Piece.PieceType == ChessPieceType.Pawn)
                        {
                            if (square.Piece.PieceColor == chessPieceColor)
                            {
                                if ((dstPosition + 7) % 8 == forceCol || forceCol == -1)
                                {
                                    return (byte)(dstPosition + 7);
                                }
                                
                            }
                        }
                    }

                    square = ChessBoard.Squares[dstPosition + 9];

                    if (square.Piece != null)
                    {
                        if (square.Piece.PieceType == ChessPieceType.Pawn)
                        {
                            if (square.Piece.PieceColor == chessPieceColor)
                            {
                                if ((dstPosition + 9) % 8 == forceCol || forceCol == -1)
                                {
                                    return (byte) (dstPosition + 9);
                                }
                            }
                        }
                    }
                }
                else 
                {
                    if (dstPosition - 7 >= 0)
                    {
                        square = ChessBoard.Squares[dstPosition - 7];

                        if (square.Piece != null)
                        {
                            if (square.Piece.PieceType == ChessPieceType.Pawn)
                            {
                                if (square.Piece.PieceColor == chessPieceColor)
                                {
                                    if ((dstPosition - 7)%8 == forceCol || forceCol == -1)
                                    {
                                        return (byte) (dstPosition - 7);
                                    }
                                }
                            }
                        }
                    }
                    if (dstPosition - 9 >= 0)
                    {
                        square = ChessBoard.Squares[dstPosition - 9];

                        if (square.Piece != null)
                        {
                            if (square.Piece.PieceType == ChessPieceType.Pawn)
                            {
                                if (square.Piece.PieceColor == chessPieceColor)
                                {
                                    if ((dstPosition - 9)%8 == forceCol || forceCol == -1)
                                    {
                                        return (byte) (dstPosition - 9);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (byte x = 0; x < 64; x++)
            {
                square = ChessBoard.Squares[x];

                if (square.Piece == null)
                    continue;
                if (square.Piece.PieceType != chessPieceType)
                    continue;
                if (square.Piece.PieceColor != chessPieceColor)
                    continue;
               
                foreach (byte move in square.Piece.ValidMoves)
                {
                    if (move == dstPosition)
                    {
                        if (!capture)
                        {
                            if ((byte)(x / 8) == (forceRow) || forceRow == -1)
                            {
                                if (x%8 == forceCol || forceCol == -1)
                                {
                                    return x;
                                }
                            }
                        }
                                
                        //Capture
                        if (ChessBoard.Squares[dstPosition].Piece != null)
                        {
                            if (ChessBoard.Squares[dstPosition].Piece.PieceColor != chessPieceColor)
                            {
                                if (x % 8 == forceCol || forceCol == -1)
                                {
                                    if ((byte)(x / 8) == (forceRow) || forceRow == -1)
                                    {
                                        return x;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return 0;
        }

        public bool IsValidMove(byte srcPosition, byte dstPosition)
        {
            if (ChessBoard == null)
            {
                return false;
            }

            if (ChessBoard.Squares == null)
            {
                return false;
            }

            if (ChessBoard.Squares[srcPosition].Piece == null)
            {
                return false;
            }

            foreach (byte bs in ChessBoard.Squares[srcPosition].Piece.ValidMoves)
            {
                if (bs == dstPosition)
                {
                    return true;
                }
            }

            if (dstPosition == ChessBoard.EnPassantPosition)
            {
                return true;
            }

            return false;
        }

        public bool IsValidMove(byte sourceColumn, byte sourceRow, byte destinationColumn, byte destinationRow)
        {
            if (ChessBoard == null)
            {
                return false;
            }

            if (ChessBoard.Squares == null)
            {
                return false;
            }

            byte index = GetBoardIndex(sourceColumn, sourceRow);

            if (ChessBoard.Squares[index].Piece == null)
            {
                return false;
            }

            foreach (byte bs in ChessBoard.Squares[index].Piece.ValidMoves)
            {
                if (bs % 8 == destinationColumn)
                {
                    if ((byte)(bs / 8) == destinationRow)
                    {
                        return true;
                    }
                }
            }

            /*index = GetBoardIndex(destinationColumn, destinationRow);

            if (index == ChessBoard.EnPassantPosition && ChessBoard.EnPassantPosition > 0)
            {
                return true;
            }*/

            return false;
        }

        public bool IsGameOver()
        {
            if (ChessBoard.StaleMate)
            {
                return true;
            }
            if (ChessBoard.WhiteMate || ChessBoard.BlackMate)
            {
                return true;
            }
            if (ChessBoard.FiftyMove >= 50)
            {
                return true;
            }
            if (ChessBoard.RepeatedMove >= 3)
            {
                return true;
            }

            if (ChessBoard.InsufficientMaterial)
            {
                return true;
            }
            return false;
        }

        public bool IsTie()
        {
            if (ChessBoard.StaleMate)
            {
                return true;
            }
            
            if (ChessBoard.FiftyMove >= 50)
            {
                return true;
            }
            if (ChessBoard.RepeatedMove >= 3)
            {
                return true;
            }

            if (ChessBoard.InsufficientMaterial)
            {
                return true;
            }

            return false;
        }
        public bool MovePiece(byte srcPosition, byte dstPosition)
        {
            Piece piece = ChessBoard.Squares[srcPosition].Piece;

            PreviousChessBoard = new Board(ChessBoard);
            UndoChessBoard = new Board(ChessBoard);
            UndoGameBook = new List<OpeningMove>(CurrentGameBook);

            Board.MovePiece(ChessBoard, srcPosition, dstPosition, PromoteToPieceType);

            ChessBoard.LastMove.GeneratePGNString(ChessBoard);

            PieceValidMoves.GenerateValidMoves(ChessBoard);
            Evaluation.EvaluateBoardScore(ChessBoard);

            //If there is a check in place, check if this is still true;
            if (piece.PieceColor == ChessPieceColor.White)
            {
                if (ChessBoard.WhiteCheck)
                {
                    //Invalid Move
                    ChessBoard = new Board(PreviousChessBoard);
                    PieceValidMoves.GenerateValidMoves(ChessBoard);
                    return false;
                }
            }
            else if (piece.PieceColor == ChessPieceColor.Black)
            {
                if (ChessBoard.BlackCheck)
                {
                    //Invalid Move
                    ChessBoard = new Board(PreviousChessBoard);
                    PieceValidMoves.GenerateValidMoves(ChessBoard);
                    return false;
                }
            }
   
            MoveHistory.Push(ChessBoard.LastMove);
            FileIO.SaveCurrentGameMove(ChessBoard, PreviousChessBoard, CurrentGameBook, ChessBoard.LastMove);

            CheckForMate(WhoseMove, ref ChessBoard);
            PieceTakenAdd(ChessBoard.LastMove);

            if (ChessBoard.WhiteMate || ChessBoard.BlackMate)
            {
                LastMove.PgnMove += "#";
            }
            else if (ChessBoard.WhiteCheck || ChessBoard.BlackCheck)
            {
                LastMove.PgnMove += "+";
            }

            return true;
        }

        private void PieceTakenAdd(MoveContent lastMove)
        {
            if (lastMove.TakenPiece.PieceType != ChessPieceType.None)
            {
                if (lastMove.TakenPiece.PieceColor == ChessPieceColor.White)
                {
                    if (lastMove.TakenPiece.PieceType == ChessPieceType.Queen)
                    {
                        PiecesTakenCount.WhiteQueen++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Rook)
                    {
                        PiecesTakenCount.WhiteRook++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Bishop)
                    {
                        PiecesTakenCount.WhiteBishop++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Knight)
                    {
                        PiecesTakenCount.WhiteKnight++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Pawn)
                    {
                        PiecesTakenCount.WhitePawn++;
                    }
                }
                if (ChessBoard.LastMove.TakenPiece.PieceColor == ChessPieceColor.Black)
                {
                    if (lastMove.TakenPiece.PieceType == ChessPieceType.Queen)
                    {
                        PiecesTakenCount.BlackQueen++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Rook)
                    {
                        PiecesTakenCount.BlackRook++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Bishop)
                    {
                        PiecesTakenCount.BlackBishop++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Knight)
                    {
                        PiecesTakenCount.BlackKnight++;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Pawn)
                    {
                        PiecesTakenCount.BlackPawn++;
                    }
                }
            }
        }

        private void PieceTakenRemove(MoveContent lastMove)
        {
            if (lastMove.TakenPiece.PieceType != ChessPieceType.None)
            {
                if (lastMove.TakenPiece.PieceColor == ChessPieceColor.White)
                {
                    if (lastMove.TakenPiece.PieceType == ChessPieceType.Queen)
                    {
                        PiecesTakenCount.WhiteQueen--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Rook)
                    {
                        PiecesTakenCount.WhiteRook--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Bishop)
                    {
                        PiecesTakenCount.WhiteBishop--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Knight)
                    {
                        PiecesTakenCount.WhiteKnight--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Pawn)
                    {
                        PiecesTakenCount.WhitePawn--;
                    }
                }
                if (lastMove.TakenPiece.PieceColor == ChessPieceColor.Black)
                {
                    if (lastMove.TakenPiece.PieceType == ChessPieceType.Queen)
                    {
                        PiecesTakenCount.BlackQueen--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Rook)
                    {
                        PiecesTakenCount.BlackRook--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Bishop)
                    {
                        PiecesTakenCount.BlackBishop--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Knight)
                    {
                        PiecesTakenCount.BlackKnight--;
                    }
                    else if (lastMove.TakenPiece.PieceType == ChessPieceType.Pawn)
                    {
                        PiecesTakenCount.BlackPawn--;
                    }
                }
            }
        }

        public bool MovePiece(byte sourceColumn, byte sourceRow, byte destinationColumn, byte destinationRow)
        {
            byte srcPosition = (byte)(sourceColumn + (sourceRow * 8));
            byte dstPosition = (byte)(destinationColumn + (destinationRow * 8));

            return MovePiece(srcPosition, dstPosition);
        }

        internal void SetChessPiece(Piece piece, byte index)
        {
            ChessBoard.Squares[index].Piece = new Piece(piece);

        }

        #endregion

        #region Search

        public void AiPonderMove()
        {
            Thinking = true;

			/* Fix added to prevent premature declaration of checkmate against computer's king.
			 * 
			 * The original version only looked at squares that were available for the black king to move to PRIOR to white's latest move.
			 * So... suppose you had a situation like this:
			 *	2QR1r1k/5ppp/8/2N5/8/8/8/K7 w - - 0 1
			 *  ... and moved the white queen from G5 to H5.
			 *	At the start of white's move, the black king has 5 possible moves... 4 of which are attackable by white, and noted by
			 *	GenerateValidMoves in WhiteAttackBoard[]. When AiPonderMove executes, it immediately eliminates those 4 squares as options
			 *	for the black king, even though 2 of them are now safe because no white piece can attack them. However, square 7 is now
			 *	directly in the queen's attack path, so it's eliminated as an option as well. Boom, premature checkmate... game over.
			 *	
			 *	The time it takes to regenerate the valid moves is nontrivial under Android. To improve performance, 
			 *	my fix regenerates the valid moves ONLY when it might prevent a game-ending premature declaration
			 *	of checkmate, unless the difficulty is set to Hard or VeryHard (in which case it runs every time). 
			 *	Even a novice player is likely to get confused if the engine declares checkmate for no apparent reason, 
			 *	but the bug's more insidious manifestations at the two easiest difficulty levels help to make the computer a tiny bit easier 
			 *	to beat by giving it a blind spot that a player can exploit, the same way HUMAN opponents have blind spots and weaknesses of their own.
			 */
			if (ChessBoard.BlackMate || ChessBoard.WhiteMate || (GameDifficulty == Difficulty.VeryHard) || (GameDifficulty == Difficulty.Hard)) {
				ChessBoard.BlackMate = false;
				ChessBoard.WhiteMate = false;
				PieceValidMoves.GenerateValidMoves(ChessBoard);
			}

            NodesSearched = 0;
			
			var resultBoards = new ResultBoards();
            resultBoards.Positions = new List<Board>();

            if (CheckForMate(WhoseMove, ref ChessBoard))
            {
                Thinking = false;
				return;
            }

            MoveContent bestMove = new MoveContent();
           
            //If there is no playbook move search for the best move
            if (FindPlayBookMove(ref bestMove, ChessBoard, OpeningBook) == false
                || ChessBoard.FiftyMove > 45 || ChessBoard.RepeatedMove >= 2)
            {
                if (FindPlayBookMove(ref bestMove, ChessBoard, CurrentGameBook) == false ||
                    ChessBoard.FiftyMove > 45 || ChessBoard.RepeatedMove >= 2)
                {
					bestMove = Search.IterativeSearch(ChessBoard, PlyDepthSearched, ref NodesSearched, ref NodesQuiessence, ref pvLine, ref PlyDepthReached, ref RootMovesSearched, CurrentGameBook);
                }
            }
 
            //Make the move 
            PreviousChessBoard = new Board(ChessBoard);

            RootMovesSearched = (byte)resultBoards.Positions.Count;

            Board.MovePiece(ChessBoard, bestMove.MovingPiecePrimary.SrcPosition, bestMove.MovingPiecePrimary.DstPosition, ChessPieceType.Queen);

            ChessBoard.LastMove.GeneratePGNString(ChessBoard);

            FileIO.SaveCurrentGameMove(ChessBoard, PreviousChessBoard, CurrentGameBook, bestMove);

            for (byte x = 0; x < 64; x++)
            {
                Square sqr = ChessBoard.Squares[x];

                if (sqr.Piece == null)
                    continue;

                sqr.Piece.DefendedValue = 0;
                sqr.Piece.AttackedValue = 0;
            }

            PieceValidMoves.GenerateValidMoves(ChessBoard);
            Evaluation.EvaluateBoardScore(ChessBoard);

            PieceTakenAdd(ChessBoard.LastMove);

            MoveHistory.Push(ChessBoard.LastMove);

            if (CheckForMate(WhoseMove, ref ChessBoard))
            {
                Thinking = false;

                if (ChessBoard.WhiteMate || ChessBoard.BlackMate)
                {
                    LastMove.PgnMove += "#";
                }
				
                return;
            }

            if (ChessBoard.WhiteCheck || ChessBoard.BlackCheck)
            {
                LastMove.PgnMove += "+";
            }

            Thinking = false;
		}

        #endregion

        #region Test

        public Test.PerformanceResult RunPerformanceTest()
        {
            return Test.RunPerfTest(5, ChessBoard);
        }

        #endregion

        #region FileIO

        public bool SaveGame(string filePath)
        {
            return FileIO.SaveGame(filePath, ChessBoard, WhoseMove, MoveHistory);
        }

        public bool LoadGame(String filePath)
        {
            return FileIO.LoadGame(filePath, ref ChessBoard, WhoseMove, ref MoveHistory, ref CurrentGameBook, ref UndoGameBook);
        }

        public bool LoadOpeningBook()
        {
            OpeningBook = Book.LoadOpeningBook();

            return true;
        }

        #endregion
   

    }
}
