namespace SMS_Search.Models.Gs1
{
    public class Gs1AiDefinition
    {
        public string Ai { get; set; } = "";
        public string Flags { get; set; } = "";
        public string Specification { get; set; } = "";
        public string Attributes { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsVariableLength { get; set; }
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
        public string DataType { get; set; } = ""; // e.g. "N", "X"
        public string ControlType { get; set; } = "Text"; // e.g. "Text", "CheckBox", "ComboBox"
        public System.Collections.Generic.List<Gs1AiOption>? Options { get; set; }
    }

    public class Gs1AiOption
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class Gs1ParseResult
    {
        public System.Collections.Generic.List<Gs1ParsedAi> ParsedAis { get; set; } = new System.Collections.Generic.List<Gs1ParsedAi>();
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string UnparsedData { get; set; } = "";
    }

    public class Gs1ParsedAi
    {
        public string Ai { get; set; } = "";
        public string RawValue { get; set; } = "";
        public Gs1AiDefinition? Definition { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class Gs1HistoryItem
    {
        public string RawValue { get; set; } = "";
        public string FormattedValue { get; set; } = "";
        public string DetectedType { get; set; } = "";
        public System.DateTime Timestamp { get; set; }
        public string OriginalAi { get; set; } = "";
    }
}
