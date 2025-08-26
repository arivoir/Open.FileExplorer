using System.Collections.Generic;

namespace Open.FileExplorer
{
    public class HardStack<T> : List<T>
    {
        public HardStack()
        {
            Maximum = int.MaxValue;
        }

        public int Maximum { get; set; }

        public T Peek()
        {
            return this[0];
        }
        
        public T Pop()
        {
            var item = this[0];
            RemoveAt(0);
            return item;
        }

        public void Push(T item)
        {
            Insert(0, item);
            while(Count > Maximum)
            {
                RemoveAt(Count - 1);
            }
        }
    }
}
