using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ConsoleApplication
{
    public class TreeNode<T>
    {
        private readonly List<TreeNode<T>> _children = new List<TreeNode<T>>();
        public T Value { get; }

        public TreeNode(T value) => Value = value;

        public TreeNode<T> this[int i] => _children[i];

        public TreeNode<T> Parent { get; private set; }

        public ReadOnlyCollection<TreeNode<T>> Children => _children.AsReadOnly();

        public TreeNode<T> AddChild(T value)
        {
            var node = new TreeNode<T>(value) {Parent = this};
            _children.Add(node);
            return node;
        }

        public TreeNode<T>[] AddChildren(params T[] values) => values.Select(AddChild).ToArray();
        public TreeNode<T>[] AddChildren(IEnumerable<T> values) => values.Select(AddChild).ToArray();

        public bool RemoveChild(TreeNode<T> node) => _children.Remove(node);

        public void Traverse(Action<T, int> action, int depth = 0)
        {
            action(Value, depth);
            foreach (var child in _children)
                child.Traverse(action, depth+1);
        }

        public IEnumerable<T> Flatten() => new[] {Value}.Concat(_children.SelectMany(x => x.Flatten()));
    }
}