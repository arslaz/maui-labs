using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace laba10.Models;

/// <summary>
/// Базовый класс для полиморфных вопросов языкового экзамена.
/// Поддерживает автоматическую полиморфную сериализацию через System.Text.Json
/// и INotifyPropertyChanged для мгновенного обновления UI при кликах.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ListeningQuestion), typeDiscriminator: "listening")]
[JsonDerivedType(typeof(MatchPairsQuestion), typeDiscriminator: "match_pairs")]
[JsonDerivedType(typeof(EssayQuestion), typeDiscriminator: "essay")]
public abstract class QuestionBase : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _title = string.Empty;
    private string _instructions = string.Empty;
    private double _maxPoints = 30.0;
    private double _earnedPoints;
    private string _feedback = string.Empty;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Instructions
    {
        get => _instructions;
        set => SetProperty(ref _instructions, value);
    }

    public abstract QuestionType Type { get; }

    public double MaxPoints
    {
        get => _maxPoints;
        set => SetProperty(ref _maxPoints, value);
    }

    public double EarnedPoints
    {
        get => _earnedPoints;
        set => SetProperty(ref _earnedPoints, value);
    }

    public string Feedback
    {
        get => _feedback;
        set => SetProperty(ref _feedback, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public abstract bool CheckIsAnswered();
    public abstract void ResetAnswer();
}
