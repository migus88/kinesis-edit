namespace KinesisEdit.Core.Keys
{
    /// <summary>
    /// Standard Windows virtual-key codes used by the key table, per specs/05-key-model.md §2.
    /// These codes are internal to the app — only file tokens are written to layout files.
    /// </summary>
    internal static class VirtualKey
    {
        public const int Back = 0x08;
        public const int Tab = 0x09;
        public const int Return = 0x0D;
        public const int Shift = 0x10;
        public const int Control = 0x11;
        public const int Menu = 0x12;
        public const int Pause = 0x13;
        public const int CapsLock = 0x14;
        public const int Escape = 0x1B;
        public const int Space = 0x20;
        public const int PageUp = 0x21;
        public const int PageDown = 0x22;
        public const int End = 0x23;
        public const int Home = 0x24;
        public const int Left = 0x25;
        public const int Up = 0x26;
        public const int Right = 0x27;
        public const int Down = 0x28;
        public const int Print = 0x2A;
        public const int Snapshot = 0x2C;
        public const int Insert = 0x2D;
        public const int Delete = 0x2E;
        public const int Digit0 = 0x30;
        public const int A = 0x41;
        public const int LeftWin = 0x5B;
        public const int RightWin = 0x5C;
        public const int Apps = 0x5D;
        public const int Numpad0 = 0x60;
        public const int Multiply = 0x6A;
        public const int Add = 0x6B;
        public const int Subtract = 0x6D;
        public const int Decimal = 0x6E;
        public const int Divide = 0x6F;
        public const int F1 = 0x70;

        // F2-F24 continue sequentially from VK_F1 (0x70); F2-F6 are named because the
        // lighting Fn-layer save-token exceptions reference them individually
        // (specs/05-key-model.md §5.5). Ranged registrations still derive from F1.
        public const int F2 = 0x71;
        public const int F3 = 0x72;
        public const int F4 = 0x73;
        public const int F5 = 0x74;
        public const int F6 = 0x75;

        public const int NumLock = 0x90;
        public const int ScrollLock = 0x91;
        public const int LeftShift = 0xA0;
        public const int RightShift = 0xA1;
        public const int LeftControl = 0xA2;
        public const int RightControl = 0xA3;
        public const int LeftMenu = 0xA4;
        public const int RightMenu = 0xA5;
        public const int VolumeMute = 0xAD;
        public const int VolumeDown = 0xAE;
        public const int VolumeUp = 0xAF;
        public const int MediaNextTrack = 0xB0;
        public const int MediaPrevTrack = 0xB1;
        public const int MediaStop = 0xB2;
        public const int MediaPlayPause = 0xB3;
        public const int Oem1 = 0xBA;
        public const int OemPlus = 0xBB;
        public const int OemComma = 0xBC;
        public const int OemMinus = 0xBD;
        public const int OemPeriod = 0xBE;
        public const int Oem2 = 0xBF;
        public const int Oem3 = 0xC0;
        public const int Oem4 = 0xDB;
        public const int Oem5 = 0xDC;
        public const int Oem6 = 0xDD;
        public const int Oem7 = 0xDE;
        public const int Oem102 = 0xE2;
    }
}
