namespace NBatch.ConsoleDemo
{
    public class Product
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}, {3:c}",
                ProductId, Name, Description, Price);
        }
    }
}