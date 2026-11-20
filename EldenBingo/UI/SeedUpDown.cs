namespace EldenBingo.UI
{
    internal sealed class SeedUpDown : NumericUpDown
    {
        protected override void UpdateEditText()
        {
            base.UpdateEditText();
            if (Value == 0)
            {
                ChangingText = true;
                Text = "Random";
            }
        }
    }
}
