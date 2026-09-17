using Escape.Data;

namespace Escape.Core
{
    /// <summary>
    /// UI facades consumed by gameplay code. The UI assembly implements and
    /// registers these; gameplay never references UI types directly.
    /// </summary>
    public interface ITerminalUI
    {
        void Open(TerminalDefinition terminal);
        void Close();
        bool IsOpen { get; }
    }

    public interface IDocumentUI
    {
        void Show(DocumentDefinition document);
        void Close();
    }

    public interface IEvidenceBoardUI
    {
        void Toggle();
        void Close();
        bool IsOpen { get; }
    }

    public interface IEndingScreenUI
    {
        void Show(EndingResult result);
    }
}
