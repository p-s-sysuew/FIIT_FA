// /TreeDataStructures/Core/BinarySearchTreeBase.cs
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode> 
    : ITree<TKey, TValue>
    where TKey : notnull
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; }
    
    public int Count { get; protected set; }
    public bool IsReadOnly => false;

    public BinarySearchTreeBase(IComparer<TKey>? comparer = null)
    {
        Comparer = comparer ?? Comparer<TKey>.Default;
    }

    #region IDictionary Implementation
    
    public virtual void Add(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        TNode newNode = CreateNode(key, value);
        
        if (Root == null)
        {
            Root = newNode;
            Count++;
            OnNodeAdded(newNode);
            return;
        }
        
        TNode current = Root;
        TNode? parent = null;
        
        while (current != null)
        {
            parent = current;
            int cmp = Comparer.Compare(key, current.Key);
            
            if (cmp < 0)
            {
                current = current.Left;
            }
            else if (cmp > 0)
            {
                current = current.Right;
            }
            else
            {
                // Key already exists, update value
                current.Value = value;
                return;
            }
        }
        
        int finalCmp = Comparer.Compare(key, parent!.Key);
        if (finalCmp < 0)
        {
            parent.Left = newNode;
        }
        else
        {
            parent.Right = newNode;
        }
        newNode.Parent = parent;
        
        Count++;
        OnNodeAdded(newNode);
    }
    
    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) return false;
        
        RemoveNode(node);
        Count--;
        return true;
    }
    
    protected virtual void RemoveNode(TNode node)
    {
        if (node.Left == null)
        {
            Transplant(node, node.Right);
            OnNodeRemoved(node.Parent, node.Right);
        }
        else if (node.Right == null)
        {
            Transplant(node, node.Left);
            OnNodeRemoved(node.Parent, node.Left);
        }
        else
        {
            TNode successor = FindMinimum(node.Right);
            
            if (successor.Parent != node)
            {
                Transplant(successor, successor.Right);
                successor.Right = node.Right;
                successor.Right.Parent = successor;
            }
            
            Transplant(node, successor);
            successor.Left = node.Left;
            successor.Left.Parent = successor;
            
            OnNodeRemoved(successor.Parent, successor);
        }
    }
    
    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }
    
    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value);
    }
    
    public ICollection<TKey> Keys
    {
        get
        {
            var keys = new List<TKey>();
            foreach (var entry in InOrder())
            {
                keys.Add(entry.Key);
            }
            return keys;
        }
    }
    
    public ICollection<TValue> Values
    {
        get
        {
            var values = new List<TValue>();
            foreach (var entry in InOrder())
            {
                values.Add(entry.Value);
            }
            return values;
        }
    }
    
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    
    public void Clear()
    {
        Root = null;
        Count = 0;
    }
    
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return TryGetValue(item.Key, out TValue? value) && 
               EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }
    
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        
        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("Array is too small");
        
        int index = arrayIndex;
        foreach (var entry in InOrder())
        {
            array[index++] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }
    
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        if (Contains(item))
            return Remove(item.Key);
        return false;
    }
    
    #endregion
    
    #region Hooks
    
    protected virtual void OnNodeAdded(TNode newNode) { }
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    #region Helpers
    
    protected abstract TNode CreateNode(TKey key, TValue value);
    
    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) return current;
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }
    
    protected TNode FindMinimum(TNode node)
    {
        TNode current = node;
        while (current.Left != null)
        {
            current = current.Left;
        }
        return current;
    }
    
    protected TNode FindMaximum(TNode node)
    {
        TNode current = node;
        while (current.Right != null)
        {
            current = current.Right;
        }
        return current;
    }
    
    #endregion
    
    #region Rotations
    
    protected void RotateLeft(TNode x)
    {
        TNode? y = x.Right;
        if (y == null) return;
        
        x.Right = y.Left;
        if (y.Left != null)
            y.Left.Parent = x;
        
        y.Parent = x.Parent;
        if (x.Parent == null)
            Root = y;
        else if (x.IsLeftChild)
            x.Parent.Left = y;
        else
            x.Parent.Right = y;
        
        y.Left = x;
        x.Parent = y;
    }
    
    protected void RotateRight(TNode y)
    {
        TNode? x = y.Left;
        if (x == null) return;
        
        y.Left = x.Right;
        if (x.Right != null)
            x.Right.Parent = y;
        
        x.Parent = y.Parent;
        if (y.Parent == null)
            Root = x;
        else if (y.IsLeftChild)
            y.Parent.Left = x;
        else
            y.Parent.Right = x;
        
        x.Right = y;
        y.Parent = x;
    }
    
    protected void RotateDoubleLeft(TNode x)
    {
        TNode? y = x.Right;
        if (y != null)
        {
            RotateRight(y);
            RotateLeft(x);
        }
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        TNode? x = y.Left;
        if (x != null)
        {
            RotateLeft(x);
            RotateRight(y);
        }
    }
    
    protected void RotateBigLeft(TNode x)
    {
        // Big left rotation is same as double left
        RotateDoubleLeft(x);
    }
    
    protected void RotateBigRight(TNode y)
    {
        // Big right rotation is same as double right
        RotateDoubleRight(y);
    }
    
    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
            Root = v;
        else if (u.IsLeftChild)
            u.Parent.Left = v;
        else
            u.Parent.Right = v;
        
        if (v != null)
            v.Parent = u.Parent;
    }
    
    #endregion
    
    #region Iterators
    
    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => 
        new TreeIterator(this, TraversalStrategy.InOrder);
    
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() => 
        new TreeIterator(this, TraversalStrategy.PreOrder);
    
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() => 
        new TreeIterator(this, TraversalStrategy.PostOrder);
    
    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() => 
        new TreeIterator(this, TraversalStrategy.InOrderReverse);
    
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() => 
        new TreeIterator(this, TraversalStrategy.PreOrderReverse);
    
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() => 
        new TreeIterator(this, TraversalStrategy.PostOrderReverse);
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var entry in InOrder())
        {
            yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    #endregion
    
    #region TreeIterator Implementation
    
    private enum TraversalStrategy
    {
        InOrder,
        PreOrder,
        PostOrder,
        InOrderReverse,
        PreOrderReverse,
        PostOrderReverse
    }
    
    private class TreeIterator : IEnumerable<TreeEntry<TKey, TValue>>, IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly BinarySearchTreeBase<TKey, TValue, TNode> _tree;
        private readonly TraversalStrategy _strategy;
        private Stack<StackFrame> _stack;
        private TreeEntry<TKey, TValue> _current;
        private bool _initialized;
        
        public TreeIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree, TraversalStrategy strategy)
        {
            _tree = tree;
            _strategy = strategy;
            _stack = new Stack<StackFrame>();
            _current = default;
            _initialized = false;
        }
        
        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;
        
        public TreeEntry<TKey, TValue> Current => _current;
        object IEnumerator.Current => Current;
        
        public bool MoveNext()
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }
            
            while (_stack.Count > 0)
            {
                var frame = _stack.Peek();
                
                if (!frame.Visited)
                {
                    ProcessNode(frame);
                }
                else
                {
                    _stack.Pop();
                    if (_strategy == TraversalStrategy.PostOrder || 
                        _strategy == TraversalStrategy.PostOrderReverse)
                    {
                        _current = new TreeEntry<TKey, TValue>(
                            frame.Node.Key, 
                            frame.Node.Value, 
                            frame.Node.Height);
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private void Initialize()
        {
            _stack.Clear();
            if (_tree.Root != null)
            {
                _stack.Push(new StackFrame(_tree.Root));
            }
        }
        
        private void ProcessNode(StackFrame frame)
        {
            frame.Visited = true;
            TNode node = frame.Node;
            
            switch (_strategy)
            {
                case TraversalStrategy.PreOrder:
                    _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, node.Height);
                    PushChildren(node, reverse: false);
                    return;
                    
                case TraversalStrategy.PreOrderReverse:
                    _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, node.Height);
                    PushChildren(node, reverse: true);
                    return;
                    
                case TraversalStrategy.InOrder:
                    PushInOrderChildren(node, reverse: false);
                    return;
                    
                case TraversalStrategy.InOrderReverse:
                    PushInOrderChildren(node, reverse: true);
                    return;
                    
                case TraversalStrategy.PostOrder:
                    PushPostOrderChildren(node, reverse: false);
                    return;
                    
                case TraversalStrategy.PostOrderReverse:
                    PushPostOrderChildren(node, reverse: true);
                    return;
            }
        }
        
        private void PushChildren(TNode node, bool reverse)
        {
            _stack.Pop(); // Remove current node
            
            if (reverse)
            {
                if (node.Left != null)
                    _stack.Push(new StackFrame(node.Left));
                if (node.Right != null)
                    _stack.Push(new StackFrame(node.Right));
            }
            else
            {
                if (node.Right != null)
                    _stack.Push(new StackFrame(node.Right));
                if (node.Left != null)
                    _stack.Push(new StackFrame(node.Left));
            }
        }
        
        private void PushInOrderChildren(TNode node, bool reverse)
        {
            _stack.Pop(); // Remove current node
            
            if (reverse)
            {
                if (node.Left != null)
                    _stack.Push(new StackFrame(node.Left));
                
                var currentFrame = new StackFrame(node);
                _stack.Push(currentFrame);
                
                if (node.Right != null)
                    _stack.Push(new StackFrame(node.Right));
            }
            else
            {
                if (node.Right != null)
                    _stack.Push(new StackFrame(node.Right));
                
                var currentFrame = new StackFrame(node);
                _stack.Push(currentFrame);
                
                if (node.Left != null)
                    _stack.Push(new StackFrame(node.Left));
            }
            
            // Mark current node as already visited for processing
            var frames = _stack.ToArray();
            _stack.Clear();
            foreach (var f in frames)
            {
                _stack.Push(f);
            }
        }
        
        private void PushPostOrderChildren(TNode node, bool reverse)
        {
            if (reverse)
            {
                if (node.Left != null)
                    _stack.Push(new StackFrame(node.Left));
                if (node.Right != null)
                    _stack.Push(new StackFrame(node.Right));
            }
            else
            {
                if (node.Right != null)
                    _stack.Push(new StackFrame(node.Right));
                if (node.Left != null)
                    _stack.Push(new StackFrame(node.Left));
            }
        }
        
        public void Reset()
        {
            Initialize();
            _initialized = true;
        }
        
        public void Dispose()
        {
            _stack.Clear();
        }
        
        private class StackFrame
        {
            public TNode Node { get; }
            public bool Visited { get; set; }
            
            public StackFrame(TNode node)
            {
                Node = node;
                Visited = false;
            }
        }
    }
    
    #endregion
}