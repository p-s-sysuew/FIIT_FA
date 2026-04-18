using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;


// Абстрактный базовый класс для всех деревьев поиска .
// Общая логика вставки/удаления/поиска,
public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) 
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{

    //Корень дерева
    protected TNode? Root;

    //Компаратор для сравнения ключей
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default;

    // Количество элементов в дереве.
    public int Count { get; protected set; }

    public bool IsReadOnly => false;

    // Возвращает все ключи в порядке возрастания (инфиксный обход).
    public ICollection<TKey> Keys
    {
        get
        {
            var list = new List<TKey>(Count);
            foreach (var entry in InOrder())
                list.Add(entry.Key);
            return list;
        }
    }

    // Возвращает все значения в порядке возрастания ключей.
    public ICollection<TValue> Values
    {
        get
        {
            var list = new List<TValue>(Count);
            foreach (var entry in InOrder())
                list.Add(entry.Value);
            return list;
        }
    }


    // Добавляет или заменяет значение по ключу.
    public virtual void Add(TKey key, TValue value)
    {
        // Случай 1: дерево пусто — создаём корень
        if (Root == null)
        {
            var newRoot = CreateNode(key, value);
            Root = newRoot;
            Count = 1;
            OnNodeAdded(newRoot);
            return;
        }

        // Случай 2: ищем место для вставки
        TNode? current = Root;
        TNode? parent = null;

        while (current != null)
        {
            parent = current;
            int compareResult = Comparer.Compare(key, current.Key);

            if (compareResult == 0) // ключ уже существует
            {
                current.Value = value;
                return;
            }
            else if (compareResult < 0)
                current = current.Left;
            else
                current = current.Right;
        }

        // Создаём новый узел и вставляем его
        var newNode = CreateNode(key, value);
        newNode.Parent = parent;

        if (Comparer.Compare(key, parent!.Key) < 0)
            parent.Left = newNode;
        else
            parent.Right = newNode;

        Count++;
        OnNodeAdded(newNode);   // хук для балансировки в наследниках
    }

    // Удаляет элемент по ключу
    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) return false;

        RemoveNode(node);
        Count--;
        return true;
    }


    // Удаляет заданный узел из дерева.
    protected virtual void RemoveNode(TNode node)
    {
        TNode? rebalanceParent;
        TNode? rebalanceChild;

        // Нет левого ребёнка
        if (node.Left == null)
        {
            rebalanceParent = node.Parent;
            rebalanceChild = node.Right;
            Transplant(node, node.Right);
            OnNodeRemoved(rebalanceParent, rebalanceChild);
            return;
        }

        // Нет правого ребёнка
        if (node.Right == null)
        {
            rebalanceParent = node.Parent;
            rebalanceChild = node.Left;
            Transplant(node, node.Left);
            OnNodeRemoved(rebalanceParent, rebalanceChild);
            return;
        }

        // Есть оба ребёнка
        var newNode = Minimum(node.Right);

        if (newNode.Parent != node)
        {
            // Минимальный узел не является прямым потомком удаляемого
            rebalanceParent = newNode.Parent;
            rebalanceChild = newNode.Right;
            Transplant(newNode, newNode.Right);   // поднимаем правого ребёнка минимального узла
            newNode.Right = node.Right;
            newNode.Right.Parent = newNode;
        }
        else
        {
            // Минимальный узел — прямой правый ребёнок
            rebalanceParent = newNode;
            rebalanceChild = newNode.Right;
        }

        // Замещаем удаляемый узел найденным минимальным
        Transplant(node, newNode);
        newNode.Left = node.Left;
        newNode.Left.Parent = newNode;

        OnNodeRemoved(rebalanceParent, rebalanceChild);
    }

    // Проверяет наличие ключа
    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;

    // Пытается получить значение по ключу
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

    // Индексатор: получение или установка значения по ключу.
    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value);
    }


    // Вызыаается после успешной вставки узла.
    protected virtual void OnNodeAdded(TNode newNode) { }

    // Вызываемый после удаления узла.
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }

    // Находит минимальный узел в поддереве
    protected static TNode Minimum(TNode node)
    {
        var current = node;
        while (current.Left != null)
            current = current.Left;
        return current;
    }

    // Метод для создания узла
    protected abstract TNode CreateNode(TKey key, TValue value);

    // Поиск узла по ключу
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

    // Малый левый поворот вокруг узла x
    protected void RotateLeft(TNode x)
    {
        var y = x.Right ?? throw new InvalidOperationException("Для левого поворота нужен правый ребёнок.");

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

    // Малый правый поворот вокруг узла y
    protected void RotateRight(TNode y)
    {
        var x = y.Left ?? throw new InvalidOperationException("Для правого поворота нужен левый ребёнок.");

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

    // Большой левый поворот: сначала левый вокруг x, затем левый вокруг нового верхнего узла
    protected void RotateBigLeft(TNode x)
    {
        RotateLeft(x);
        var newTop = x.Parent;
        if (newTop?.Right != null)
            RotateLeft(newTop);
    }

    // Большой правый поворот: сначала правый вокруг y, затем правый вокруг нового верхнего узла
    protected void RotateBigRight(TNode y)
    {
        RotateRight(y);
        var newTop = y.Parent;
        if (newTop?.Left != null)
            RotateRight(newTop);
    }

    // Двойной левый поворот: левый вокруг левого ребёнка x, затем правый вокруг x
    protected void RotateDoubleLeft(TNode x)
    {
        var left = x.Left ?? throw new InvalidOperationException("Для двойного левого поворота нужен x.Left != null");
        if (left.Right == null)
            throw new InvalidOperationException("Для двойного левого поворота нужен x.Left.Right != null");

        RotateLeft(left);
        RotateRight(x);
    }

    // Двойной правый поворот: правый вокруг правого ребёнка y, затем левый вокруг y
    protected void RotateDoubleRight(TNode y)
    {
        var right = y.Right ?? throw new InvalidOperationException("Для двойного правого поворота нужен y.Right != null");
        if (right.Left == null)
            throw new InvalidOperationException("Для двойного правого поворота нужен y.Right.Left != null");

        RotateRight(right);
        RotateLeft(y);
    }

    // Заменяет поддерево с корнем u на поддерево с корнем v.
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

    // Итераторы

    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => new TreeTraversal(Root, TraversalStrategy.InOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() => new TreeTraversal(Root, TraversalStrategy.PreOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() => new TreeTraversal(Root, TraversalStrategy.PostOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() => new TreeTraversal(Root, TraversalStrategy.InOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() => new TreeTraversal(Root, TraversalStrategy.PreOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() => new TreeTraversal(Root, TraversalStrategy.PostOrderReverse);

    // Обёртка для итератора, реализующая IEnumerable
    private sealed class TreeTraversal : IEnumerable<TreeEntry<TKey, TValue>>
    {
        private readonly TNode? _root;
        private readonly TraversalStrategy _strategy;

        public TreeTraversal(TNode? root, TraversalStrategy strategy) { _root = root; _strategy = strategy; }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => new TreeIterator(_root, _strategy);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TreeIterator : IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly TNode? _root;
        private readonly int _targetStage; 
        private readonly bool _reverse;
        private TNode? _currentNode;
        private TNode? _previousNode;
        private TreeEntry<TKey, TValue> _current;
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

        // Переход к следующему элементу
        public bool MoveNext()
        {
            while (_currentNode != null)
            {
                TNode node = _currentNode;

                TNode? first = _reverse ? node.Right : node.Left;
                TNode? second = _reverse ? node.Left : node.Right;

                // Случай 1: пришли от родителя
                if (_previousNode == node.Parent)
                {
                    if (_targetStage == 0) // Pre-order: отдаём узел сразу
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

                    if (_targetStage == 1) // In-order: отдаём после левого поддерева
                    {
                        _current = MakeEntry(node);
                        _previousNode = node;
                        _currentNode = second ?? node.Parent;
                        return true;
                    }

                    if (second == null) // Post-order и нет правого поддерева
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

                // Случай 2: пришли от первого ребёнка
                if (_previousNode == first)
                {
                    if (_targetStage == 1) // In-order
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

                    if (_targetStage == 2) // Post-order
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

                // Случай 3: пришли от второго ребёнка
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

        public void Dispose() { }

        private TreeEntry<TKey, TValue> MakeEntry(TNode node) =>
            new TreeEntry<TKey, TValue>(node.Key, node.Value, Height(node));

        // Вычисляет высоту узла
        private int Height(TNode? node)
        {
            if (node == null) return 0;
            if (_heights.TryGetValue(node, out int h)) return h;

            h = 1 + Math.Max(Height(node.Left), Height(node.Right));
            _heights[node] = h;
            return h;
        }

        // Преобразует стратегию обхода в параметры итератора
        private static void ConvertStrategy(TraversalStrategy input, out int targetStage, out bool reverse)
        {
            switch (input)
            {
                case TraversalStrategy.InOrder: reverse = false; targetStage = 1; break;
                case TraversalStrategy.InOrderReverse: reverse = true; targetStage = 1; break;
                case TraversalStrategy.PreOrder: reverse = false; targetStage = 0; break;
                case TraversalStrategy.PreOrderReverse: reverse = true; targetStage = 2; break;
                case TraversalStrategy.PostOrder: reverse = false; targetStage = 2; break;
                case TraversalStrategy.PostOrderReverse: reverse = true; targetStage = 0; break;
                default: reverse = false; targetStage = 1; break;
            }
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        InOrder().Select(e => new KeyValuePair<TKey, TValue>(e.Key, e.Value)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex > array.Length)
            throw new ArgumentException("Недостаточно места в массиве.");

        foreach (var entry in InOrder())
            array[arrayIndex++] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}