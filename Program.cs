using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Pong
{
    /// <summary>
    /// This class holds the start point for the chosen project "Pong".
    /// </summary>
    /// <remarks>
    /// Before anything starts, the Console will be adjusted to fit the Arena.
    /// </remarks>
    internal static class Program
    {
        internal static int Main()
        {
            Console.CursorVisible = false;

            // https://docs.microsoft.com/en-us/dotnet/api/system.console.setbuffersize?view=netframework-4.8
            // Set the smallest possible window size before setting the buffer size.
            Console.SetWindowSize(1, 1);
            Console.SetBufferSize(121, 31);
            Console.SetWindowSize(121, 31);
            Console.OutputEncoding = Encoding.Unicode;

            GameController pongGameController = new GameController();
            pongGameController.ShowLogo();
            pongGameController.Init();

            Console.ReadKey();
            return 0;
        }
    }

    #region GameController

    /// <summary>
    /// This class creates and controls every object in the game.
    /// </summary>
    internal class GameController
    {
        private readonly Arena GameArena = new Arena(0, 0);
        internal static readonly NetworkController networkController = new NetworkController();
        internal static readonly Player PlayerLeft = new Player(3, 15);
        internal static readonly Player PlayerRight = new Player(116, 15);
        // Coords: 60, 15
        internal static readonly Ball PlayBall = new Ball(
            checked((byte)(Console.WindowWidth / 2)), checked((byte)(Console.WindowHeight / 2)));
        private static readonly Score ScorePlayerLeft = new Score(2, 2);
        private static readonly Score ScorePlayerRight = new Score(117, 2);
        internal static bool IsGameRunning { get; set; }
        internal static bool IsRoundRunning { get; set; }

        private ConsoleKey GetGameModeSelection()
        {
            var ListOfAllowedConsoleKeys = new List<ConsoleKey>
            {
                ConsoleKey.D1,
                ConsoleKey.NumPad1,
                ConsoleKey.D2,
                ConsoleKey.NumPad2
            };

            Console.Write("Please choose if you want to host or join a game:\n");
            Console.Write("1. Host\n");
            Console.Write("2. Join\n");
            ConsoleKey pressedKey;
            do
            {
                pressedKey = Console.ReadKey(true).Key;
            } while (!ListOfAllowedConsoleKeys.Contains(pressedKey));

            return pressedKey;
        }

        internal static void HandleGoal(Player.Side playerSide)
        {
            IsRoundRunning = false;
            PlayBall.ResetPosition();
            switch (playerSide)
            {
                case Player.Side.LEFT:
                    ScorePlayerRight.Increase();
                    break;
                case Player.Side.RIGHT:
                    ScorePlayerLeft.Increase();
                    break;
            }

            if (ScorePlayerLeft.Value == 3)
            {
                IsGameRunning = false;
                ShowWinnerInfo(PlayerLeft.Name);
            }
            else if (ScorePlayerRight.Value == 3)
            {
                IsGameRunning = false;
                ShowWinnerInfo(PlayerRight.Name);
            }
            else
            {
                PlayBall.Appear();
                // Start next round after 3 seconds.
                Thread.Sleep(3000);
                StartNextRound();
            }
        }

        internal void Init()
        {
            var gameModeSelection = GetGameModeSelection();
            Console.Write(string.Format("Your Choice: {0}\n", gameModeSelection));
            switch (gameModeSelection)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    {
                        PlayerLeft.IsActive = true;
                        break;
                    }
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    {
                        PlayerRight.IsActive = true;
                        break;
                    }
            }
            Console.WriteLine(string.Format("Local Endpoint: {0}", networkController.LocalEndPoint));
            networkController.Connect();
            StartGame();
        }

        private static void PrintCenteredText(string pText)
        {
            Console.Write(new string(' ', (Console.WindowWidth - pText.Length) / 2));
            Console.WriteLine(pText);
        }

        internal void ShowLogo()
        {
            Console.Clear();
            Console.WriteLine();
            PrintCenteredText("      ___         ___           ___           ___     ");
            PrintCenteredText("     /  /\\       /  /\\         /__/\\         /  /\\    ");
            PrintCenteredText("    /  /::\\     /  /::\\        \\  \\:\\       /  /:/_   ");
            PrintCenteredText("   /  /:/\\:\\   /  /:/\\:\\        \\  \\:\\     /  /:/ /\\  ");
            PrintCenteredText("  /  /:/~/:/  /  /:/  \\:\\   _____\\__\\:\\   /  /:/_/::\\ ");
            PrintCenteredText(" /__/:/ /:/  /__/:/ \\__\\:\\ /__/::::::::\\ /__/:/__\\/\\:\\");
            PrintCenteredText(" \\  \\:\\/:/   \\  \\:\\ /  /:/ \\  \\:\\~~\\~~\\/ \\  \\:\\ /~~/:/");
            PrintCenteredText("  \\  \\::/     \\  \\:\\  /:/   \\  \\:\\  ~~~   \\  \\:\\  /:/ ");
            PrintCenteredText("   \\  \\:\\      \\  \\:\\/:/     \\  \\:\\        \\  \\:\\/:/  ");
            PrintCenteredText("    \\  \\:\\      \\  \\::/       \\  \\:\\        \\  \\::/   ");
            PrintCenteredText("     \\__\\/       \\__\\/         \\__\\/         \\__\\/    ");
            Console.WriteLine();
        }

        internal static void ShowWinnerInfo(string playerName)
        {
            PrintCenteredText(string.Format("{0} wins!", playerName));
        }

        private void StartGame()
        {
            IsGameRunning = true;
            IsRoundRunning = true;

            GameArena.Appear();
            PlayBall.Appear();
            PlayerLeft.PlayerPaddle.Appear();
            PlayerRight.PlayerPaddle.Appear();
            ScorePlayerLeft.Appear();
            ScorePlayerRight.Appear();
            // TODO: Let the Player choose his Name and send it to the other Player.
            PlayerLeft.Name = "Left Player";
            PlayerRight.Name = "Right Player";

            PlayBall.StartMovingThread();
            if (PlayerLeft.IsActive)
                PlayerLeft.PlayerPaddle.StartMovingThread();
            else if (PlayerRight.IsActive)
                PlayerRight.PlayerPaddle.StartMovingThread();
        }

        private static void StartNextRound()
        {
            IsRoundRunning = true;
        }
    }

    #endregion

    #region GameObject Base Class

    /// <summary>
    /// Base class for every game object.
    /// </summary>
    internal class GameObject
    {
        // Byte values can be sent without the need to convert it before.
        internal byte PositionX { get; set; }
        internal byte PositionY { get; set; }
        protected char Symbol { get; set; }

        internal GameObject(byte posX, byte posY)
        {
            PositionX = posX;
            PositionY = posY;
        }

        internal virtual void Appear()
        {
            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(Symbol);
        }
    }

    #endregion

    #region Score

    internal class Score : GameObject
    {
        internal int Value { get; set; }

        internal Score(byte posX, byte posY) : base(posX, posY)
        {
            PositionX = posX;
            PositionY = posY;
        }

        internal override void Appear()
        {
            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(Value);
        }

        internal void Increase()
        {
            Value++;

            Appear();
        }
    }

    #endregion

    #region Moveable (GameObject Specialization)

    /// <summary>
    /// Base class for every movable GameObject.
    /// </summary>
    internal abstract class Movable : GameObject
    {
        protected byte Speed { get; set; }
        internal MovementDirection CurrentMovementDirection { get; set; }
        protected Thread MovementThread { get; set; }

        protected Movable(byte posX, byte posY) : base(posX, posY)
        {
            Speed = 1;
        }

        internal enum MovementDirection
        {
            DOWN = 1,
            LEFT_DOWN = 2,
            LEFT = 3,
            LEFT_UP = 4,
            RIGHT_DOWN = 5,
            RIGHT = 6,
            RIGHT_UP = 7,
            UP = 8
        }

        internal void LeaveCurrentPosition()
        {
            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(' ');
        }

        protected bool IsLeftWallCollision(byte newPosX)
        {
            return checked(newPosX - 1) <= Arena.BorderLeft;
        }

        protected bool IsRightWallCollision(byte newPosX)
        {
            return checked(newPosX + 3) >= Arena.BorderRight;
        }

        protected bool IsTopWallCollision(byte newPosY)
        {
            return checked(newPosY - 1) <= Arena.BorderTop;
        }

        protected bool IsBottomWallCollision(byte newPosY)
        {
            return checked(newPosY + 3) >= Arena.BorderBottom;
        }

        protected bool IsLeftPaddleExtensionDownCollision(byte newPosX, byte newPosY)
        {
            return checked(newPosX - 1) == GameController.PlayerLeft.PlayerPaddle.PositionX
                && newPosY == GameController.PlayerLeft.PlayerPaddle.PosYExtensionDown;
        }

        protected bool IsLeftPaddleCenterCollision(byte newPosX, byte newPosY)
        {
            return checked(newPosX - 1) == GameController.PlayerLeft.PlayerPaddle.PositionX
                && newPosY == GameController.PlayerLeft.PlayerPaddle.PositionY;
        }

        protected bool IsLeftPaddleExtensionUpCollision(byte newPosX, byte newPosY)
        {
            return checked(newPosX - 1) == GameController.PlayerLeft.PlayerPaddle.PositionX
                && newPosY == GameController.PlayerLeft.PlayerPaddle.PosYExtensionUp;
        }

        protected bool IsRightPaddleExtensionDownCollision(byte newPosX, byte newPosY)
        {
            return checked(newPosX + 1) == GameController.PlayerRight.PlayerPaddle.PositionX
                && newPosY == GameController.PlayerRight.PlayerPaddle.PosYExtensionDown;
        }

        protected bool IsRightPaddleCenterCollision(byte newPosX, byte newPosY)
        {
            return checked(newPosX + 1) == GameController.PlayerRight.PlayerPaddle.PositionX
                && newPosY == GameController.PlayerRight.PlayerPaddle.PositionY;
        }

        protected bool IsRightPaddleExtensionUpCollision(byte newPosX, byte newPosY)
        {
            return checked(newPosX + 1) == GameController.PlayerRight.PlayerPaddle.PositionX
                && newPosY == GameController.PlayerRight.PlayerPaddle.PosYExtensionUp;
        }

        protected bool IsLeftPaddleCollision(byte newPosX, byte newPosY)
        {
            return IsLeftPaddleExtensionDownCollision(newPosX, newPosY)
                || IsLeftPaddleCenterCollision(newPosX, newPosY)
                || IsLeftPaddleExtensionUpCollision(newPosX, newPosY);
        }

        protected bool IsRightPaddleCollision(byte newPosX, byte newPosY)
        {
            return IsRightPaddleExtensionDownCollision(newPosX, newPosY)
                || IsRightPaddleCenterCollision(newPosX, newPosY)
                || IsRightPaddleExtensionUpCollision(newPosX, newPosY);
        }

        protected bool IsPaddleCollision(byte newPosX, byte newPosY)
        {
            return IsLeftPaddleCollision(newPosX, newPosY)
                || IsRightPaddleCollision(newPosX, newPosY);
        }

        protected bool IsWallCollision(byte newPosX, byte newPosY)
        {
            // BUG: Random symbol drawing when holding the button after reaching the top/bottom border.
            return IsLeftWallCollision(newPosX)
                || IsRightWallCollision(newPosX)
                || IsTopWallCollision(newPosY)
                || IsBottomWallCollision(newPosY);
        }

        // Default movement with bounce behaviour after collision with Paddle or Wall.
        internal virtual void Move(byte newPosX, byte newPosY)
        {
            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(' ');

            switch (CurrentMovementDirection)
            {
                case MovementDirection.LEFT:
                    if (IsLeftWallCollision(newPosX))
                    {
                        PositionX++;
                        CurrentMovementDirection = MovementDirection.RIGHT;
                    }
                    else PositionX--;
                    break;
                case MovementDirection.RIGHT:
                    if (IsRightWallCollision(newPosX))
                    {
                        PositionX--;
                        CurrentMovementDirection = MovementDirection.LEFT;
                    }
                    else PositionX++;
                    break;
                case MovementDirection.UP:
                    if (IsTopWallCollision(newPosY))
                    {
                        PositionY++;
                        CurrentMovementDirection = MovementDirection.DOWN;
                    }
                    else PositionY--;
                    break;
                case MovementDirection.DOWN:
                    if (IsBottomWallCollision(newPosY))
                    {
                        PositionY--;
                        CurrentMovementDirection = MovementDirection.UP;
                    }
                    else PositionY++;
                    break;
            }
            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(Symbol);
        }

        internal virtual void ResetPosition()
        {
            PositionX = 0;
            PositionY = 0;
        }

        internal virtual void StartMoving()
        {
        }

        internal void StartMovingThread()
        {
            MovementThread = new Thread(StartMoving);
            MovementThread.Start();
        }
    }

    #endregion

    #region Arena GameObject

    /// <summary>
    /// The Arena is defined here.
    /// </summary>
    // TODO: Should be Singleton.
    internal class Arena : GameObject
    {
        internal static readonly int BorderBottom = Console.WindowHeight;
        internal static readonly int BorderLeft = Console.WindowLeft;
        internal static readonly int BorderRight = Console.WindowWidth;
        internal static readonly int BorderTop = Console.WindowTop;

        internal Arena(byte posX, byte posY) : base(posX, posY)
        {
        }

        internal override void Appear()
        {
            Console.SetCursorPosition(PositionX, PositionY);
            // Draw upper border.
            for (var i = BorderLeft; i < BorderRight - 1; i++)
            {
                Console.SetCursorPosition(PositionX++, PositionY);
                Console.Write('-');
            }

            // Reset position and draw left border.
            PositionX = 0;
            PositionY = 1;
            Console.SetCursorPosition(PositionX, PositionY);
            for (var i = BorderTop; i < BorderBottom - 2; i++)
            {
                Console.SetCursorPosition(PositionX, PositionY++);
                Console.Write('|');
            }

            // Reset position and draw right border.
            PositionX = 119;
            PositionY = 1;
            Console.SetCursorPosition(PositionX, PositionY);
            for (var i = BorderTop; i < BorderBottom - 2; i++)
            {
                Console.SetCursorPosition(PositionX, PositionY++);
                Console.Write('|');
            }

            // Reset position and draw bottom border
            PositionX = 0;
            PositionY = 29;
            Console.SetCursorPosition(PositionX, PositionY);
            for (var i = BorderLeft; i < BorderRight - 1; i++)
            {
                // draw the symbol...
                Console.SetCursorPosition(PositionX++, PositionY);
                Console.Write('-');
            }
        }
    }

    #endregion

    #region Ball (Game Object<-Movable)

    /// <summary>
    /// Specialization of a Movable (GameObject)
    /// </summary>
    internal class Ball : Movable
    {
        internal Ball(byte posX, byte posY) : base(posX, posY)
        {
            Symbol = 'o';
            // 25 is already too fast to react in the Console App context.
            Speed = 9;
        }

        internal override void Move(byte newPosX, byte newPosY)
        {
            LeaveCurrentPosition();

            if (IsWallCollision(newPosX, newPosY))
            {
                if (IsBottomWallCollision(newPosY))
                {
                    if (CurrentMovementDirection == MovementDirection.LEFT_DOWN)
                    {
                        CurrentMovementDirection = MovementDirection.LEFT_UP;
                        GameController.networkController.SendMovablePositionUpdate(this,
                            MovementDirection.LEFT_UP, --PositionX, --PositionY);
                    }
                    else if (CurrentMovementDirection == MovementDirection.RIGHT_DOWN)
                    {
                        CurrentMovementDirection = MovementDirection.RIGHT_UP;
                        GameController.networkController.SendMovablePositionUpdate(this,
                            MovementDirection.RIGHT_UP, ++PositionX, --PositionY);
                    }
                    return;
                }

                if (IsTopWallCollision(newPosY))
                {
                    if (CurrentMovementDirection == MovementDirection.LEFT_UP)
                    {
                        CurrentMovementDirection = MovementDirection.LEFT_DOWN;
                        GameController.networkController.SendMovablePositionUpdate(this,
                            MovementDirection.LEFT_DOWN, --PositionX, ++PositionY);
                    }
                    else if (CurrentMovementDirection == MovementDirection.RIGHT_UP)
                    {
                        CurrentMovementDirection = MovementDirection.RIGHT_DOWN;
                        GameController.networkController.SendMovablePositionUpdate(this,
                            MovementDirection.RIGHT_DOWN, ++PositionX, ++PositionY);
                    }
                    return;
                }

                if (IsLeftWallCollision(newPosX))
                {
                    GameController.HandleGoal(Player.Side.LEFT);
                    return;
                }

                if (IsRightWallCollision(newPosX))
                {
                    GameController.HandleGoal(Player.Side.RIGHT);
                    return;
                }
            }
            else if (IsPaddleCollision(newPosX, newPosY))
            {
                if (IsLeftPaddleCollision(newPosX, newPosY))
                {
                    LeftPaddleBounce(newPosX, newPosY);
                    return;
                }

                if (IsRightPaddleCollision(newPosX, newPosY))
                {
                    RightPaddleBounce(newPosX, newPosY);
                    return;
                }
            }

            switch (CurrentMovementDirection)
            {
                case MovementDirection.LEFT:
                    PositionX--;
                    break;
                case MovementDirection.LEFT_DOWN:
                    PositionX--;
                    PositionY++;
                    break;
                case MovementDirection.LEFT_UP:
                    PositionX--;
                    PositionY--;
                    break;
                // Note: A wierd blue stroke-track appears when moving to the right while using a symbol like ♥ .
                // (Vanishes after moving the left Paddle.)
                case MovementDirection.RIGHT:
                    PositionX++;
                    break;
                case MovementDirection.RIGHT_DOWN:
                    PositionX++;
                    PositionY++;
                    break;
                case MovementDirection.RIGHT_UP:
                    PositionX++;
                    PositionY--;
                    break;
            }

            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(Symbol);
        }

        private void LeftPaddleBounce(byte newPosX, byte newPosY)
        {
            if (IsLeftPaddleExtensionDownCollision(newPosX, newPosY))
            {
                CurrentMovementDirection = MovementDirection.RIGHT_DOWN;
                GameController.networkController.SendMovablePositionUpdate(this,
                    MovementDirection.RIGHT_DOWN, ++PositionX, ++PositionY);
            }
            else if (IsLeftPaddleCenterCollision(newPosX, newPosY))
            {
                CurrentMovementDirection = MovementDirection.RIGHT;
                GameController.networkController.SendMovablePositionUpdate(this,
                    MovementDirection.RIGHT, ++PositionX, PositionY);
            }
            else if (IsLeftPaddleExtensionUpCollision(newPosX, newPosY))
            {
                CurrentMovementDirection = MovementDirection.RIGHT_UP;
                GameController.networkController.SendMovablePositionUpdate(this,
                    MovementDirection.RIGHT_UP, ++PositionX, --PositionY);
            }
        }

        private void RightPaddleBounce(byte newPosX, byte newPosY)
        {
            if (IsRightPaddleExtensionDownCollision(newPosX, newPosY))
            {
                CurrentMovementDirection = MovementDirection.LEFT_DOWN;
                GameController.networkController.SendMovablePositionUpdate(this,
                    MovementDirection.LEFT_DOWN, --PositionX, ++PositionY);
            }
            else if (IsRightPaddleCenterCollision(newPosX, newPosY))
            {
                CurrentMovementDirection = MovementDirection.LEFT;
                GameController.networkController.SendMovablePositionUpdate(this,
                    MovementDirection.LEFT, --PositionX, PositionY);
            }
            else if (IsRightPaddleExtensionUpCollision(newPosX, newPosY))
            {
                CurrentMovementDirection = MovementDirection.LEFT_UP;
                GameController.networkController.SendMovablePositionUpdate(this,
                    MovementDirection.LEFT_UP, --PositionX, --PositionY);
            }
        }

        internal override void ResetPosition()
        {
            PositionX = checked((byte)(Console.WindowWidth / 2));
            PositionY = checked((byte)(Console.WindowHeight / 2));
        }

        internal override void StartMoving()
        {
            CurrentMovementDirection = MovementDirection.RIGHT;
            while (GameController.IsGameRunning && GameController.IsRoundRunning)
            {
                Move(PositionX, PositionY);
                Thread.Sleep(1000 / Speed);
            }
        }
    }

    #endregion

    #region Paddle (Game Object<-Movable)

    /// <summary>
    /// Specialization of a Movable (GameObject).
    /// </summary>
    internal sealed class Paddle : Movable
    {
        internal byte PosYExtensionUp { get; set; }
        internal byte PosYExtensionDown { get; set; }

        internal Paddle(byte posX, byte posY) : base(posX, posY)
        {
            PosYExtensionDown = checked((byte)(PositionY + 1));
            PosYExtensionUp = checked((byte)(PositionY - 1));
            Speed = 25;
            Symbol = 'I';
        }

        internal override void Move(byte newPosX, byte newPosY)
        {
            if (IsWallCollision(newPosX, newPosY)) return;

            // Clear old PosYExtensionDown.
            Console.SetCursorPosition(PositionX, PosYExtensionDown);
            Console.Write(' ');
            // Clear old PositionY.
            LeaveCurrentPosition();
            // Clear old PosYExtensionUp.
            Console.SetCursorPosition(PositionX, PosYExtensionUp);
            Console.Write(' ');
            // Set new values for PosYExtensionDown and PosYExtensionUp
            PosYExtensionDown = checked((byte)(newPosY + 1));
            PositionY = newPosY;
            PosYExtensionUp = checked((byte)(newPosY - 1));

            // Shouldn't change.
            PositionX = newPosX;

            // Draw the symbol on the new Position.
            Console.SetCursorPosition(PositionX, PosYExtensionDown);
            Console.Write(Symbol);
            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(Symbol);
            Console.SetCursorPosition(PositionX, PosYExtensionUp);
            Console.Write(Symbol);
        }

        internal void ProcessPlayerInput(ConsoleKey pressedKey)
        {
            switch (pressedKey)
            {
                case ConsoleKey.UpArrow:
                    CurrentMovementDirection = MovementDirection.UP;
                    Move(PositionX, checked((byte)(PositionY - 1)));
                    GameController.networkController.SendMovablePositionUpdate(this, CurrentMovementDirection, PositionX, PositionY);
                    break;
                case ConsoleKey.DownArrow:
                    CurrentMovementDirection = MovementDirection.DOWN;
                    Move(PositionX, checked((byte)(PositionY + 1)));
                    GameController.networkController.SendMovablePositionUpdate(this, CurrentMovementDirection, PositionX, PositionY);
                    break;
                default:
                    break;
            }
        }

        internal override void Appear()
        {
            Console.SetCursorPosition(PositionX, PosYExtensionDown);
            Console.Write(Symbol);
            Console.SetCursorPosition(PositionX, PositionY);
            Console.Write(Symbol);
            Console.SetCursorPosition(PositionX, PosYExtensionUp);
            Console.Write(Symbol);
        }

        internal override void StartMoving()
        {
            // https://stackoverflow.com/questions/45393158/detecting-single-key-presses-in-console-c-sharp
            while (GameController.IsGameRunning)
            {
                ProcessPlayerInput(Console.ReadKey(true).Key);
            }
        }
    }

    #endregion

    #region Player Class with its own Paddle

    /// <summary>
    /// 
    /// </summary>
    internal class Player
    {
        internal readonly Paddle PlayerPaddle;
        internal bool IsActive = false;
        internal string Name { get; set; }

        internal Player(byte posX, byte posY)
        {
            PlayerPaddle = new Paddle(posX, posY);
        }

        internal enum Side
        {
            LEFT,
            RIGHT
        }
    }

    #endregion

    #region Logging Functionalities

    internal class Logger
    {
        private readonly StreamWriter sw;

        internal Logger(string className)
        {
            sw = OpenStream(string.Format("{0}.log", className));
        }

        internal void Log(string msg)
        {
            // https://docs.microsoft.com/en-us/dotnet/api/system.io.streamwriter.writelineasync?view=netframework-4.8
            sw.WriteLineAsync(string.Format("{0}: {1}", DateTime.Now, msg));
        }

        // https://docs.microsoft.com/de-de/dotnet/standard/io/handling-io-errors
        private StreamWriter OpenStream(string path)
        {
            try
            {
                var fs = new FileStream(path, FileMode.Append);
                return new StreamWriter(fs);
            }
            catch (IOException e)
            {
                Console.WriteLine(string.Format("An exception occurred:\nError code: {0}\nMessage: {1}",
                    e.HResult & 0x0000FFFF, e.Message));
            }
            return null;
        }
    }

    #endregion

    #region Network Controller (Connection handling between players)

    internal class NetworkController
    {
        private readonly Logger NetworkControllerLogger;

        internal bool IsUserInputIpAddress { get; set; }
        private const string IpAddressHostPart = "192.168.2.";
        internal readonly UdpConnection udpConnection;
        internal readonly IPEndPoint LocalEndPoint;
        internal IPEndPoint RemoteIpEndPoint { get; set; }
        internal const int Port = 19082;
        internal readonly Socket ConnectionSocket;

        private readonly Thread ThreadReceiveMovablePositionUpdate;
        private static readonly object _lock = new object();

        internal NetworkController()
        {
            NetworkControllerLogger = NetworkControllerLogger = new Logger(ToString());

            udpConnection = new UdpConnection();
            LocalEndPoint = new IPEndPoint(IPAddress.Any, Port);
            ConnectionSocket = new Socket(LocalEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            ThreadReceiveMovablePositionUpdate = new Thread(ReceiveMovablePositionUpdate);
        }

        enum Participant
        {
            BALL = 1,
            PADDLE = 2
        }

        internal void Connect()
        {
            ConnectionSocket.Bind(LocalEndPoint);

            IsUserInputIpAddress = true;
            if (IsUserInputIpAddress)
            {
                GetRemoteIpEndPointFromUserInput();
                Console.Write(string.Format("Trying to connect to...{0}", RemoteIpEndPoint));
            }
            else
            {
                udpConnection.UdpSenderThread.Start();
                udpConnection.UdpReceiverThread.Start();
            }

            while (!ConnectionSocket.Connected)
            {
                try
                {
                    if (RemoteIpEndPoint != null)
                    {
                        // TODO: Version check!
                        ConnectionSocket.Connect(RemoteIpEndPoint);
                        if (ConnectionSocket.Connected)
                        {
                            if (!IsUserInputIpAddress)
                            {
                                udpConnection.UdpSenderThread.Abort();
                                udpConnection.UdpReceiverThread.Abort();
                            }
                            // Clear the console after all the connection attempts...
                            Console.Clear();
                            PrintSysMessage(string.Format("Connected to: {0}", ConnectionSocket.RemoteEndPoint));
                            NetworkControllerLogger.Log(string.Format("Connected to: {0}", ConnectionSocket.RemoteEndPoint));
                            ThreadReceiveMovablePositionUpdate.Start();
                        }
                    }
                }
                catch (SocketException e)
                {
                    // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socketerror?view=netframework-4.8
                    NetworkControllerLogger.Log(string.Format("Could not connect to {0}; Error: {1}, {2}",
                        RemoteIpEndPoint, e.SocketErrorCode, e.ErrorCode));
                    Thread.Sleep(1000);
                    Console.Write(".");
                }
            }
        }

        private void GetRemoteIpEndPointFromUserInput()
        {
            Console.WriteLine("Please enter the IP-Address of the other player: ");
            StringBuilder ipAddressInput = new StringBuilder();
            // Predefined IpAddressHostPart for easier testing purposes.
            ipAddressInput.Append(IpAddressHostPart);
            IPAddress parsedIpAddress;
            do
            {
                Console.Write(ipAddressInput.ToString());
                ipAddressInput.Append(Console.ReadLine());
                // BUG: Predefined IpAddressHostPart can't be deleted (with Backspace).
                IPAddress.TryParse(ipAddressInput.ToString(), out parsedIpAddress);
                if (parsedIpAddress == null)
                    Console.WriteLine("IpAddress is not valid.");
            } while (parsedIpAddress == null);
            RemoteIpEndPoint = new IPEndPoint(parsedIpAddress, Port);
        }

        private void PrintSysMessage(string msg)
        {
            Console.SetCursorPosition(1, 30);
            Console.Write(msg.PadRight(Console.WindowWidth - 2, ' '));
        }

        private void ReceiveMovablePositionUpdate()
        {
            // https://docs.microsoft.com/en-us/dotnet/framework/network-programming/network-tracing
            const int FIONREAD = 0x4004667F;
            while (ConnectionSocket.Connected)
            {
                // https://stackoverflow.com/questions/38681856/using-thread-sleep-in-lock-section-c-sharp
                lock (_lock)
                {
                    try
                    {
                        // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.available?view=netframework-4.8#System_Net_Sockets_Socket_Available
                        byte[] outValue = BitConverter.GetBytes(0);
                        ConnectionSocket.IOControl(FIONREAD, null, outValue);
                        uint bytesAvailable = BitConverter.ToUInt32(outValue, 0);
                        Trace.WriteLine(string.Format("ConnectionSocket has {0} bytes pending. Available property says {1}.",
                            bytesAvailable, ConnectionSocket.Available));

                        byte[] buffer = new byte[4];
                        int receivedBytes = ConnectionSocket.Receive(buffer);
                        byte receivedSenderId = buffer[0];
                        byte receivedMovementDirectionId = buffer[1];
                        byte receivedPosX = buffer[2];
                        byte receivedPosY = buffer[3];

                        NetworkControllerLogger.Log(string.Format(
                            "Received: receivedSenderId {0}, receivedMovementDirectionId {1}, receivedPosX {2}, receivedPosY {3} ({4} receivedBytes))",
                            receivedSenderId, receivedMovementDirectionId, receivedPosX, receivedPosY, receivedBytes));

                        // https://www.dotnetperls.com/enum-parse
                        if (!Enum.TryParse(receivedSenderId.ToString(), out Participant receivedParticipant)) continue;
                        if (!Enum.TryParse(receivedMovementDirectionId.ToString(), out Movable.MovementDirection receivedMovementDirection)) continue;

                        // Packet is for other Paddle.
                        if (receivedParticipant == Participant.PADDLE)
                        {
                            if (GameController.PlayerLeft.IsActive)
                            {
                                if (GameController.PlayerRight.PlayerPaddle.PositionX == receivedPosX
                                    && GameController.PlayerRight.PlayerPaddle.PositionY == receivedPosY)
                                    continue;

                                GameController.PlayerRight.PlayerPaddle.CurrentMovementDirection = receivedMovementDirection;
                                GameController.PlayerRight.PlayerPaddle.Move(GameController.PlayerRight.PlayerPaddle.PositionX, receivedPosY);
                            }
                            else if (GameController.PlayerRight.IsActive)
                            {
                                if (GameController.PlayerLeft.PlayerPaddle.PositionX == receivedPosX
                                    && GameController.PlayerLeft.PlayerPaddle.PositionY == receivedPosY)
                                    continue;

                                GameController.PlayerLeft.PlayerPaddle.CurrentMovementDirection = receivedMovementDirection;
                                GameController.PlayerLeft.PlayerPaddle.Move(GameController.PlayerLeft.PlayerPaddle.PositionX, receivedPosY);
                            }
                        }
                        // Packet is for PlayBall.
                        else if (receivedParticipant == Participant.BALL)
                        {
                            if (GameController.PlayBall.PositionX == receivedPosX
                                && GameController.PlayBall.PositionY == receivedPosY)
                                continue;

                            GameController.PlayBall.CurrentMovementDirection = receivedMovementDirection;
                            GameController.PlayBall.Move(receivedPosX, receivedPosY);
                        }
                    }
                    catch (SocketException e)
                    {
                        // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socketerror?view=netframework-4.8
                        NetworkControllerLogger.Log(string.Format("Error receiving position update: {0}, {1}",
                        e.SocketErrorCode, e.ErrorCode));
                    }
                }
            }
        }

        internal void SendMovablePositionUpdate(Movable movable, Movable.MovementDirection movementDirection, byte posX, byte posY)
        {
            if (ConnectionSocket.Connected)
            {
                byte senderId = 0;
                if (movable is Ball) senderId = (byte)Participant.BALL;
                else if (movable is Paddle) senderId = (byte)Participant.PADDLE;
                if (senderId > 0)
                {
                    try
                    {
                        byte[] buffer = { senderId, (byte)movementDirection, posX, posY };
                        int sentBytes = ConnectionSocket.Send(buffer);
                        NetworkControllerLogger.Log(string.Format(
                            "Sent: senderId {0}, movementDirection {1}, posX {2}, posY {3}, ({4} sentBytes)",
                        buffer[0], buffer[1], buffer[2], buffer[3], sentBytes));
                    }
                    catch (SocketException e)
                    {
                        // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socketerror?view=netframework-4.8
                        NetworkControllerLogger.Log(string.Format("Failed sending movable position update: {0}, {1}",
                        e.SocketErrorCode, e.ErrorCode));
                    }
                }
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/api/system.threading.manualresetevent?view=netframework-4.8
        // ManualResetEvent is used to block and release threads manually.
        // It is created in the unsignaled state.
        private static readonly ManualResetEvent SendDone = new ManualResetEvent(false);
        private static readonly ManualResetEvent ReceiveDone = new ManualResetEvent(false);

        // https://docs.microsoft.com/en-us/dotnet/framework/network-programming/using-an-asynchronous-client-socket
        internal void SendAsyncMovablePositionUpdate(Movable movable, Movable.MovementDirection movementDirection, byte posX, byte posY)
        {
            if (ConnectionSocket.Connected)
            {
                byte senderId = 0;
                if (movable is Ball) senderId = (byte)Participant.BALL;
                else if (movable is Paddle) senderId = (byte)Participant.PADDLE;
                if (senderId > 0)
                {
                    try
                    {
                        byte[] buffer = { senderId, (byte)movementDirection, posX, posY };

                        // Begin sending the data to the remote device.  
                        ConnectionSocket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None,
                            new AsyncCallback(SendCallback), ConnectionSocket);
                    }
                    catch (SocketException e)
                    {
                        // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socketerror?view=netframework-4.8
                        NetworkControllerLogger.Log(string.Format("Failed sending movable position update: {0}, {1}",
                        e.SocketErrorCode, e.ErrorCode));
                    }
                }
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                NetworkControllerLogger.Log(string.Format("Sent {0} bytes to server.", bytesSent));

                // Signal that all bytes have been sent.  
                SendDone.Set();
            }
            catch (SocketException e)
            {
                // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socketerror?view=netframework-4.8
                NetworkControllerLogger.Log(string.Format("Error receiving position update: {0}, {1}",
                e.SocketErrorCode, e.ErrorCode));
                NetworkControllerLogger.Log(string.Format("{0}", e.ToString()));
            }
        }

        internal void ReceiveAsyncMovablePositionUpdate()
        {
            // Create the state object.  
            StateObject state = new StateObject
            {
                workSocket = ConnectionSocket
            };

            // https://docs.microsoft.com/en-us/dotnet/framework/network-programming/network-tracing
            const int FIONREAD = 0x4004667F;
            while (ConnectionSocket.Connected)
            {
                // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.available?view=netframework-4.8#System_Net_Sockets_Socket_Available
                byte[] outValue = BitConverter.GetBytes(0);
                ConnectionSocket.IOControl(FIONREAD, null, outValue);
                uint bytesAvailable = BitConverter.ToUInt32(outValue, 0);
                Trace.WriteLine(string.Format("ConnectionSocket has {0} bytes pending. Available property says {1}.",
                    bytesAvailable, ConnectionSocket.Available));

                // Begin receiving the data from the remote device.  
                ConnectionSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);

                byte receivedSenderId = 0;
                byte receivedMovementDirectionId = 0;
                byte receivedPosX = 0;
                byte receivedPosY = 0;
                int receivedBytes = 0;

                receivedSenderId = state.buffer[0];
                receivedMovementDirectionId = state.buffer[1];
                receivedPosX = state.buffer[2];
                receivedPosY = state.buffer[3];
                NetworkControllerLogger.Log(string.Format(
                    "Received: receivedSenderId {0}, receivedMovementDirectionId {1}, receivedPosX {2}, receivedPosY {3} ({4} receivedBytes))",
                    receivedSenderId, receivedMovementDirectionId, receivedPosX, receivedPosY, receivedBytes));

                // https://www.dotnetperls.com/enum-parse
                if (!Enum.TryParse(receivedSenderId.ToString(), out Participant receivedParticipant)) continue;
                if (!Enum.TryParse(receivedMovementDirectionId.ToString(), out Movable.MovementDirection receivedMovementDirection)) continue;

                // Packet is for other Paddle.
                if (receivedParticipant == Participant.PADDLE)
                {
                    if (GameController.PlayerLeft.IsActive)
                    {
                        if (GameController.PlayerRight.PlayerPaddle.PositionX == receivedPosX
                            && GameController.PlayerRight.PlayerPaddle.PositionY == receivedPosY)
                            continue;

                        GameController.PlayerRight.PlayerPaddle.CurrentMovementDirection = receivedMovementDirection;
                        GameController.PlayerRight.PlayerPaddle.Move(GameController.PlayerRight.PlayerPaddle.PositionX, receivedPosY);
                    }
                    else if (GameController.PlayerRight.IsActive)
                    {
                        if (GameController.PlayerLeft.PlayerPaddle.PositionX == receivedPosX
                            && GameController.PlayerLeft.PlayerPaddle.PositionY == receivedPosY)
                            continue;

                        GameController.PlayerLeft.PlayerPaddle.CurrentMovementDirection = receivedMovementDirection;
                        GameController.PlayerLeft.PlayerPaddle.Move(GameController.PlayerLeft.PlayerPaddle.PositionX, receivedPosY);
                    }
                }
                // Packet is for PlayBall.
                else if (receivedParticipant == Participant.BALL)
                {
                    if (GameController.PlayBall.PositionX == receivedPosX
                        && GameController.PlayBall.PositionY == receivedPosY)
                        continue;

                    GameController.PlayBall.CurrentMovementDirection = receivedMovementDirection;
                    GameController.PlayBall.Move(receivedPosX, receivedPosY);
                }

                ReceiveDone.Reset();
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;

                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);
                if (bytesRead > 0)
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                // Signal that all bytes have been received.
                else ReceiveDone.Set();
            }
            catch (SocketException e)
            {
                // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socketerror?view=netframework-4.8
                NetworkControllerLogger.Log(string.Format("Error receiving position update: {0}, {1}",
                e.SocketErrorCode, e.ErrorCode));
                NetworkControllerLogger.Log(string.Format("{0}", e.ToString()));
            }
        }

        private class StateObject
        {
            // Client socket.
            internal Socket workSocket = null;
            // Size of receive buffer.
            internal const int BufferSize = 256;
            // Receive buffer.
            internal byte[] buffer = new byte[BufferSize];
        }
    }

    #endregion

    #region UdpConnection handler to get the IP-Address of the unknown opponent

    internal class UdpConnection
    {
        private readonly Logger UdpConnectionLogger;

        private const int listenerPort = 11000;
        internal readonly Thread UdpReceiverThread;
        internal readonly Thread UdpSenderThread;

        internal UdpConnection()
        {
            UdpConnectionLogger = new Logger(ToString());

            UdpReceiverThread = new Thread(StartListeningForUdpBroadcastPackets);
            UdpSenderThread = new Thread(StartSendingUdpBroadcastPackets);
        }

        // TODO: Verify unstatic approach listening for udp broadcast packets.
        // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.udpclient.receive?view=netframework-4.8#examples
        internal void StartListeningForUdpBroadcastPackets()
        {
            // Creates a UdpClient for reading incoming data.
            UdpClient receivingUdpClient = new UdpClient(listenerPort)
            {
                EnableBroadcast = true
            };

            // Creates an IPEndPoint to record the IP Address and port number of the sender. 
            // The IPEndPoint will allow you to read datagrams sent from any source.
            IPEndPoint MeetingPoint = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                bool localIpMatchesRemoteIp;
                Console.Write(string.Format("Started listening at: {0}", MeetingPoint.Address));
                while (!GameController.networkController.ConnectionSocket.Connected)
                {
                    localIpMatchesRemoteIp = false;
                    Console.Write(".");
                    // Blocks until a message returns on this socket from a remote host.
                    byte[] receivedBytes = receivingUdpClient.Receive(ref MeetingPoint);
                    IPAddress remoteIpAddress = MeetingPoint.Address;
                    IPHostEntry localhost = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in localhost.AddressList)
                        if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString() == remoteIpAddress.ToString())
                        {
                            UdpConnectionLogger.Log(string.Format("Received data from local machine: {0}", MeetingPoint));
                            localIpMatchesRemoteIp = true;
                            break;
                        }

                    if (!localIpMatchesRemoteIp)
                    {
                        IPHostEntry remotehost = Dns.GetHostEntry(MeetingPoint.Address);
                        UdpConnectionLogger.Log(string.Format(
                        "{0} (LocalEndPoint: {1}) discovered: {2} (RemoteEndPoint: {3}; Received: {4}",
                        localhost.HostName, receivingUdpClient.Client.LocalEndPoint,
                        remotehost.HostName, MeetingPoint, Encoding.ASCII.GetString(receivedBytes)));
                        GameController.networkController.RemoteIpEndPoint =
                            new IPEndPoint(MeetingPoint.Address, NetworkController.Port);
                    }
                    Thread.Sleep(1000);
                }
            }
            catch (SocketException e)
            {
                // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socketerror?view=netframework-4.8
                UdpConnectionLogger.Log(string.Format("Error while listening: {0}, {1}", e.SocketErrorCode, e.ErrorCode));
            }
            finally
            {
                receivingUdpClient.Close();
            }
        }

        // https://docs.microsoft.com/en-gb/dotnet/api/system.net.sockets.socket.sendto?view=netframework-4.8
        internal void StartSendingUdpBroadcastPackets()
        {
            // Subnet specific: IPAddress.Parse("192.168.2.255")
            IPAddress broadcast = IPAddress.Broadcast; // = 255.255.255.255
            IPEndPoint remoteEndpoint = new IPEndPoint(broadcast, listenerPort);
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                EnableBroadcast = true
            };

            byte[] msgToSend = Encoding.ASCII.GetBytes("Hello World!");
            Console.WriteLine(string.Format("Started broadcasting at: {0}", broadcast));
            while (!GameController.networkController.ConnectionSocket.Connected)
            {
                // This call blocks.
                serverSocket.SendTo(msgToSend, remoteEndpoint);
                UdpConnectionLogger.Log(string.Format("Broadcast sent: {0}", msgToSend));
                Thread.Sleep(1000);
            }
        }

        // TODO: Just one method to discover other player.
        private void DiscoverOtherPlayer()
        {
            IPAddress broadcast = IPAddress.Parse("192.168.2.255");
            UdpClient server = new UdpClient(listenerPort);

            // Alternative: Broadcast IPAddress as string.
            byte[] responseData = Encoding.ASCII.GetBytes(GameController.networkController.LocalEndPoint.ToString());
            IPEndPoint clientEndpoint = new IPEndPoint(broadcast, 0);
            var clientRequestData = server.Receive(ref clientEndpoint);
            var clientRequest = Encoding.ASCII.GetString(clientRequestData);

            try
            {
                while (!GameController.networkController.ConnectionSocket.Connected)
                {
                    if (clientRequestData != null)
                    {
                        Console.WriteLine(string.Format("Received {0} from {1}, sending response.",
                            clientRequest, clientEndpoint.Address.ToString()));
                        server.Send(responseData, responseData.Length, clientEndpoint);
                        UdpConnectionLogger.Log(string.Format("Broadcast sent: {0}", responseData));
                    }
                    Thread.Sleep(1000);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                server.Close();
            }
        }
    }
    #endregion

}
