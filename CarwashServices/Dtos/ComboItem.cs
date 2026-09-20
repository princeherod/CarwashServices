using CarwashServices.Dtos;
namespace CarwashServices.Dtos

{
    // ============================================================
    // Combo box helper — carries an optional ID + display text.
    // ============================================================

    public class ComboItem
    {
        public int? Id { get; }
        public string Text { get; }

        public ComboItem(int? id, string text)
        {
            Id = id;
            Text = text;
        }

        public override string ToString() => Text;
    }
}