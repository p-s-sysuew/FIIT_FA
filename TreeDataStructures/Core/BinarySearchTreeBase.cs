using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) 
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default; // use it to compare Keys

    public int Count { get; protected set; }
    
    public bool IsReadOnly => false;

    public ICollection<TKey> Keys // Implemented
    {
        get
        {
            var list = new List<TKey>(Count);
            foreach (var entry in InOrder())
            {
                list.Add(entry.Key);
            }

            return list;
        }
    }
    public ICollection<TValue> Values // Implemented
    {
        get
        {
            var list = new List<TValue>(Count);
            foreach (var entry in InOrder())
            {
                list.Add(entry.Value);
            }

            return list;
        }
    }
    
    
    public virtual void Add(TKey key, TValue value) // Implemented
    {
        if (Root == null)
        {
            var newRoot = CreateNode(key, value);
            Root = newRoot;
            Count = 1;
            OnNodeAdded(newRoot);
            return;
        }

        TNode? current = Root;
        TNode? parent = null;

        while (current != null)
        {
            parent = current;
            int compareResult = Comparer.Compare(key, current.Key);

            if (compareResult == 0) // Если найден двойник, then just replacing it.
            {
                current.Value = value;
                return;
            }
            else if (compareResult < 0)
            {
                current = current.Left;
            }
            else
            {
                current = current.Right;
            }
        }

        var newNode = CreateNode(key, value);
        newNode.Parent = parent;

        if (Comparer.Compare(key, parent!.Key) < 0)
        {
            parent!.Left = newNode;
        }
        else
        {
            parent!.Right = newNode;
        }

        Count++;
        OnNodeAdded(newNode);
    }

    
    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) { return false; }

        RemoveNode(node);
        this.Count--;
        return true;
    }
    
    
    protected virtual void RemoveNode(TNode node) // Implemented
    {
        TNode? rebalanceParent;
        TNode? rebalanceChild;

        if (node.Left == null)
        {
            rebalanceParent = node.Parent;
            rebalanceChild = node.Right;

            Transplant(node, node.Right);
            OnNodeRemoved(rebalanceParent, rebalanceChild);
            return;
        }

        if (node.Right == null)
        {
            rebalanceParent = node.Parent;
            rebalanceChild = node.Left;

            Transplant(node, node.Left);
            OnNodeRemoved(rebalanceParent, rebalanceChild);
            return;
        }

        var newNode = Minimum(node.Right);

        if (newNode.Parent != node)
        {
            rebalanceParent = newNode.Parent;
            rebalanceChild = newNode.Right;

            Transplant(newNode, newNode.Right);

            newNode.Right = node.Right;
            newNode.Right.Parent = newNode;
        }
        else
        {
            rebalanceParent = newNode;
            rebalanceChild = newNode.Right;
        }

        Transplant(node, newNode);

        newNode.Left = node.Left;
        newNode.Left.Parent = newNode;

        OnNodeRemoved(rebalanceParent, rebalanceChild);
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

    
    #region Hooks
    
    /// <summary>
    /// Вызывается после успешной вставки
    /// </summary>
    /// <param name="newNode">Узел, который встал на место</param>
    protected virtual void OnNodeAdded(TNode newNode) { }

    /// <summary>
    /// Вызывается после удаления. 
    /// </summary>
    /// <param name="parent">Узел, чей ребенок изменился</param>
    /// <param name="child">Узел, который встал на место удаленного</param>
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    
    #region Helpers
    
    protected static TNode Minimum(TNode node) // Added
    {
        var current = node;

        while (current.Left != null)
        {
            current = current.Left;
        }

        return current;
    }
    protected abstract TNode CreateNode(TKey key, TValue value);


    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) { return current; }
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    // Rotations vvv

    protected void RotateLeft(TNode x)
    {
        var y = x.Right ?? throw new InvalidOperationException("Для поворота налево необходимо наличие у узла правого поддерева.");

        x.Right = y.Left;
        if (y.Left != null)
        {
            y.Left.Parent = x;
        }

        y.Parent = x.Parent;

        if (x.Parent == null)
        {
            Root = y;
        }
        else if (x.IsLeftChild)
        {
            x.Parent.Left = y;
        }
        else
        {
            x.Parent.Right = y;
        }

        y.Left = x;
        x.Parent = y;
    }

    protected void RotateRight(TNode y)
    {
        var x = y.Left ?? throw new InvalidOperationException("Для поворота направо необходимо наличие у узла левого поддерева.");

        y.Left = x.Right;
        if (x.Right != null)
        {
            x.Right.Parent = y;
        }

        x.Parent = y.Parent;

        if (y.Parent == null)
        {
            Root = x;
        }
        else if (y.IsLeftChild)
        {
            y.Parent.Left = x;
        }
        else
        {
            y.Parent.Right = x;
        }

        x.Right = y;
        y.Parent = x;
    }
    
    protected void RotateBigLeft(TNode x)
    {
        RotateLeft(x);

        var newTop = x.Parent;
        if (newTop?.Right != null)
        {
            RotateLeft(newTop);
        }
    }

    protected void RotateBigRight(TNode y)
    {
        RotateRight(y);

        var newTop = y.Parent;
        if (newTop?.Left != null)
        {
            RotateRight(newTop);
        }
    }
    
    protected void RotateDoubleLeft(TNode x)
    {
        var left = x.Left ?? throw new InvalidOperationException("Не выполнены условия для успешного совершения поворота (x.Left != null)");
        if (left.Right == null)
        {
            throw new InvalidOperationException("Не выполнены условия для успешного совершения поворота (x.Left.Right != null)");
        }

        RotateLeft(left);

        RotateRight(x);
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        var right = y.Right ?? throw new InvalidOperationException("Не выполнены условия для успешного совершения поворота (y.Right != null)");
        if (right.Left == null)
        {
            throw new InvalidOperationException("Не выполнены условия для успешного совершения поворота (y.Right.Left != null)");
        }

        RotateRight(right);

        RotateLeft(y);
    }

    protected void Transplant(TNode u, TNode? v) // Potential bug fix
    {
        var parent = u.Parent;

        if (u.Parent == null)
        {
            Root = v;
        }
        else if (u.IsLeftChild)
        {
            u.Parent.Left = v;
        }
        else
        {
            u.Parent.Right = v;
        }
        
        v?.Parent = u.Parent;
    }
    #endregion
    
    // Iterators vvv

    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => new TreeTraversal(Root, TraversalStrategy.InOrder); 
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrder() => new TreeTraversal(Root, TraversalStrategy.PreOrder); 
    public IEnumerable<TreeEntry<TKey, TValue>>  PostOrder() => new TreeTraversal(Root, TraversalStrategy.PostOrder); 
    public IEnumerable<TreeEntry<TKey, TValue>>  InOrderReverse() => new TreeTraversal(Root, TraversalStrategy.InOrderReverse); 
    public IEnumerable<TreeEntry<TKey, TValue>>  PreOrderReverse() => new TreeTraversal(Root, TraversalStrategy.PreOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() => new TreeTraversal(Root, TraversalStrategy.PostOrderReverse); 
    
    private sealed class TreeTraversal : IEnumerable<TreeEntry<TKey, TValue>> // Обёртка
    {
        private readonly TNode? _root;
        private readonly TraversalStrategy _strategy;

        public TreeTraversal(TNode? root, TraversalStrategy strategy)
        {
            _root = root;
            _strategy = strategy;
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => new TreeIterator(_root, _strategy);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Внутренний класс-итератор. 
    /// Реализует паттерн Iterator вручную, без yield return (ban).
    /// </summary>
    private sealed class TreeIterator : IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly TNode? _root;
        private readonly int _targetStage; // 0pre | 1in | 2post
        private readonly bool _reverse;
        private TNode? _currentNode;
        private TNode? _previousNode;
        private TreeEntry<TKey, TValue> _current; // ОтдаваемыйЭлемент
        private readonly Dictionary<TNode, int> _heights = new();

        public TreeIterator(TNode? root, TraversalStrategy strategy)
        {
            _root = root;
            ConvertStrategy(strategy, out _targetStage, out _reverse);
            _currentNode = _root;
            _previousNode = null;
            _current = default;
        }

        public TreeEntry<TKey, TValue> Current => _current;
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            while (_currentNode != null)
            {
                TNode node = _currentNode;

                TNode? first = _reverse ? node.Right : node.Left;
                TNode? second = _reverse ? node.Left : node.Right;

                if (_previousNode == node.Parent)
                {
                    if (_targetStage == 0)
                    {
                        _current = MakeEntry(node);

                        _previousNode = node;
                        _currentNode = first ?? second ?? node.Parent;

                        return true;
                    }

                    if (first != null)
                    {
                        _previousNode = node;
                        _currentNode = first;
                        continue;
                    }

                    if (_targetStage == 1)
                    {
                        _current = MakeEntry(node);

                        _previousNode = node;
                        _currentNode = second ?? node.Parent;
                        return true;
                    }

                    if (second == null)
                    {
                        _current = MakeEntry(node);

                        _previousNode = node;
                        _currentNode = node.Parent;
                        return true;
                    }

                    _previousNode = node;
                    _currentNode = second;
                    continue;
                }

                if (_previousNode == first)
                {
                    if (_targetStage == 1)
                    {
                        _current = MakeEntry(node);

                        _previousNode = node;
                        _currentNode = second ?? node.Parent;
                        return true;
                    }

                    if (second != null)
                    {
                        _previousNode = node;
                        _currentNode = second;
                        continue;
                    }

                    if (_targetStage == 2)
                    {
                        _current = MakeEntry(node);

                        _previousNode = node;
                        _currentNode = node.Parent;
                        return true;
                    }

                    _previousNode = node;
                    _currentNode = node.Parent;
                    continue;
                }

                if (_targetStage == 2)
                {
                    _current = MakeEntry(node);

                    _previousNode = node;
                    _currentNode = node.Parent;
                    return true;
                }

                _previousNode = node;
                _currentNode = node.Parent;
            }

            return false;
        }

        public void Reset()
        {
            _currentNode = _root;
            _previousNode = null;
            _current = default;
            _heights.Clear();
        }

        public void Dispose()
        {
        }

        private TreeEntry<TKey, TValue> MakeEntry(TNode node) => new TreeEntry<TKey, TValue>(node.Key, node.Value, Height(node));

        private int Height(TNode? node)
        {
            if (node == null)
            {
                return 0;
            }

            if (_heights.TryGetValue(node, out int h))
            {
                return h;
            }

            h = 1 + Math.Max(Height(node.Left), Height(node.Right));

            _heights[node] = h;

            return h;
        }

        private static void ConvertStrategy(TraversalStrategy inputStrata, out int targetStage, out bool reverse)
        {
            switch (inputStrata)
            {
                case TraversalStrategy.InOrder:
                    reverse = false;
                    targetStage = 1;
                    break;
                case TraversalStrategy.InOrderReverse:
                    reverse = true;
                    targetStage = 1;
                    break;
                case TraversalStrategy.PreOrder:
                    reverse = false;
                    targetStage = 0;
                    break;
                case TraversalStrategy.PreOrderReverse:
                    reverse = true;
                    targetStage = 2;
                    break;
                case TraversalStrategy.PostOrder:
                    reverse = false;
                    targetStage = 2;
                    break;
                case TraversalStrategy.PostOrderReverse:
                    reverse = true;
                    targetStage = 0;
                    break;
                default:
                    reverse = false;
                    targetStage = 1;
                    break;
            } // Magic.
        }
    }
    
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return InOrder().Select(element => new KeyValuePair<TKey, TValue>(element.Key, element.Value)).GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) // Implemented
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            throw new ArgumentException("Не достатоно места в массиве.");
        }

        foreach (var entry in InOrder())
        {
            array[arrayIndex++] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}