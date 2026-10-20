namespace HSKMoreHardcore
{
    /// <summary>
    /// Отладочный лог мода. По умолчанию выключен — в обычной игре мод молчит,
    /// в Player.log попадают только предупреждения и ошибки (несработавший патч,
    /// изменившееся API чужого мода).
    ///
    /// Чтобы увидеть, какие патчи встали и как они отработали, поставить
    /// Enabled = true и пересобрать. Подсистемы с собственным подробным логом
    /// (ManhunterDoorBreak, TechCountDebug) включаются своими константами
    /// DebugLog — они независимы от этого флага.
    ///
    /// Поле не const: с константой компилятор ругался бы на недостижимый код
    /// в каждом вызове.
    /// </summary>
    public static class HMHLog
    {
        public static readonly bool Enabled = false;

        public static void Message(string text)
        {
            if (Enabled)
                Verse.Log.Message(text);
        }
    }
}
