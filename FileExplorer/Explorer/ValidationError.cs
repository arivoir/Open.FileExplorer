namespace Open.FileExplorer
{
    public class ValidationError
    {
        public ValidationError(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
