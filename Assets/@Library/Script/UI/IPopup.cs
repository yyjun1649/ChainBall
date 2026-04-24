
    using Library;

    public interface IPopup
    {
        void Show();
        void Hide();
        bool IsActive { get; }
    }
