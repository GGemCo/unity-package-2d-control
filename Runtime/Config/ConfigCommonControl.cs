namespace GGemCo2DControl
{
    public static class ConfigCommonControl
    {
        public enum ExecutionOrdering
        {
            None,
            Control = 1000,
            Auto = 2000
        }

        public const string NameControlSchemePc = "Keyboard&Mouse";
        public const string NameControlSchemeGamepad = "Gamepad";

        public const string NameActionMove = "Move";
        public const string NameActionAttack = "Attack";
        public const string NameActionJump = "Jump";
    }
}