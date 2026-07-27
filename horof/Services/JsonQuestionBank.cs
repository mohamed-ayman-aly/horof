using System.Text.Json;

namespace horof.Services;

public class JsonQuestionBank : IQuestionBank
{
    private readonly Dictionary<char, List<TriviaQuestion>> _byLetter = new();
    private readonly Dictionary<char, int> _cursor = new();

    public JsonQuestionBank()
    {
        LoadFromEmbedded();
    }

    public TriviaQuestion GetQuestion(char letter)
    {
        var key = NormalizeLetter(letter);
        if (!_byLetter.TryGetValue(key, out var list) || list.Count == 0)
        {
            return new TriviaQuestion(
                Guid.NewGuid().ToString("N"),
                key,
                $"سؤال: ما كلمة تبدأ بحرف «{key}»؟",
                "إجابة تبدأ بنفس الحرف");
        }

        if (!_cursor.ContainsKey(key))
            _cursor[key] = 0;

        var q = list[_cursor[key] % list.Count];
        _cursor[key]++;
        return q;
    }

    private static char NormalizeLetter(char letter) =>
        char.ToUpperInvariant(letter);

    private void LoadFromEmbedded()
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("questions.json").GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var items = JsonSerializer.Deserialize<List<QuestionDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Letter) || string.IsNullOrWhiteSpace(item.Text))
                    continue;

                var letter = NormalizeLetter(item.Letter[0]);
                if (!_byLetter.ContainsKey(letter))
                    _byLetter[letter] = [];

                _byLetter[letter].Add(new TriviaQuestion(
                    item.Id ?? Guid.NewGuid().ToString("N"),
                    letter,
                    item.Text,
                    item.AnswerHint ?? ""));
            }
        }
        catch
        {
            SeedFallbackQuestions();
        }
    }

    private void SeedFallbackQuestions()
    {
        Add('ص', "ما الحيوان الذي يُلقب بسفينة الصحراء؟", "جمل");
        Add('س', "ما عاصمة المملكة العربية السعودية؟", "الرياض");
        Add('م', "ما الكوكب الأحمر؟", "المريخ");
        Add('د', "ما اللون الذي يرمز للسماء صافية؟", "أزرق — للتمرين: د");
    }

    private void Add(char letter, string text, string hint)
    {
        letter = NormalizeLetter(letter);
        if (!_byLetter.ContainsKey(letter))
            _byLetter[letter] = [];

        _byLetter[letter].Add(new TriviaQuestion(Guid.NewGuid().ToString("N"), letter, text, hint));
    }

    private sealed class QuestionDto
    {
        public string? Id { get; set; }
        public string? Letter { get; set; }
        public string? Text { get; set; }
        public string? AnswerHint { get; set; }
    }
}
