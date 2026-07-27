namespace horof.Services;

public record TriviaQuestion(string Id, char Letter, string Text, string AnswerHint);

public interface IQuestionBank
{
    TriviaQuestion GetQuestion(char letter);
}
