namespace TreeToolKit;

public interface IEscapeClosableWindow
{
    bool IsOpen { get; }

    bool IsFocused { get; }

    void Close();
}
