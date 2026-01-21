using System.Text.RegularExpressions;

namespace TMSpeech.Translator.APITranslator.Utils
{
    public static class RegexPatterns
    {
        // Remove the `.` between two uppercase letters. (Cope with acronym)
        private static readonly Regex _acronym = new(@"([A-Z])\s*\.\s*([A-Z])(?![A-Za-z]+)", RegexOptions.Compiled);
        public static Regex Acronym() => _acronym;

        // If an acronym is followed by a word, preserve the space between them.
        private static readonly Regex _acronymWithWords = new(@"([A-Z])\s*\.\s*([A-Z])(?=[A-Za-z]+)", RegexOptions.Compiled);
        public static Regex AcronymWithWords() => _acronymWithWords;

        // Remove redundant spaces and `\n` around punctuation.
        private static readonly Regex _punctuationSpace = new(@"\s*([.!?,])\s*", RegexOptions.Compiled);
        public static Regex PunctuationSpace() => _punctuationSpace;

        // If it is Chinese or Japanese punctuation, no need to insert spaces.
        private static readonly Regex _cjPunctuationSpace = new(@"\s*([。！？，、])\s*", RegexOptions.Compiled);
        public static Regex CJPunctuationSpace() => _cjPunctuationSpace;

        private static readonly Regex _noticePrefixAndTranslation = new(@"^(\[.+\] )?(.*)$", RegexOptions.Compiled);
        public static Regex NoticePrefixAndTranslation() => _noticePrefixAndTranslation;

        private static readonly Regex _noticePrefix = new(@"^\[.+\] ", RegexOptions.Compiled);
        public static Regex NoticePrefix() => _noticePrefix;

        private static readonly Regex _httpPrefix = new(@"^(https?:\/\/)", RegexOptions.Compiled);
        public static Regex HttpPrefix() => _httpPrefix;

        private static readonly Regex _multipleSlashes = new(@"\/{2,}", RegexOptions.Compiled);
        public static Regex MultipleSlashes() => _multipleSlashes;

        private static readonly Regex _modelThinking = new(@"<think>.*?<\/think>", RegexOptions.Compiled | RegexOptions.Singleline);
        public static Regex ModelThinking() => _modelThinking;

        private static readonly Regex _versionNumber = new(@"[^0-9.]", RegexOptions.Compiled);
        public static Regex VersionNumber() => _versionNumber;
        
        private static readonly Regex _targetSentence = new(@"<\[(.*)\]>", RegexOptions.Compiled);
        public static Regex TargetSentence() => _targetSentence;
    }
}
