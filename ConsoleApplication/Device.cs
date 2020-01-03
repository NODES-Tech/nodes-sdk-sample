namespace ConsoleApplication
{
    public class Device
    {
        public string Id { get; set; }
        public string AssetPortfolioId { get; set; }
        public float InitialLoad { get; set; }
        public float CurrentLoad { get; set; }
        public string Name { get; set; }

        public override string ToString() => $"{Name} ({Id})";
    }
}