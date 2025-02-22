﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if !COMPILER_UDONSHARP
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("UdonSharp.Editor")]
#endif

namespace UdonSharp.Lib.Internal.Collections
{
    internal class ListIterator<T> : IEnumerator
    #if COMPILER_UDONSHARP
        where T : IComparable
    #endif
    {
        private List<T> _list;
        private int _index;
        private T _current;
        
        public ListIterator(List<T> list)
        {
            _list = list;
            _index = -1;
            _current = default;
        }
        
        public bool MoveNext()
        {
            List<T> list = _list;
            int size = list._size;
            int index = _index + 1;
            
            if (index < size)
            {
                T[] items = list._items;
                
                _index = index;
                _current = items[index];
                return true;
            }
            
            _index = size;
            _current = default;
            return false;
        }

        public void Reset()
        {
            _index = -1;
            _current = default;
        }

        object IEnumerator.Current => Current;

        public T Current => _current;
    }
    
    internal class List<T> : IEnumerable
    // This is a hack to support the Sort method, it's not actually a hard constraint and is expected to error during compilation on the Sort method if the type doesn't implement IComparable
    // We don't want this in actual C# land because it causes issues constructing the List with a type that doesn't implement IComparable which kills the compiler
    #if COMPILER_UDONSHARP 
        where T : IComparable
    #endif
    {
        internal T[] _items = new T[8];
        internal int _size;

        public void Add(T item)
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (size == itemArr.Length)
            {
                T[] newItems = new T[itemArr.Length * 2];
                System.Array.Copy(itemArr, newItems, itemArr.Length);
                _items = newItems;
                itemArr = newItems;
            }
            
            itemArr[_size] = item;
            _size = size + 1;
        }
        
        private int[] _sortStack;
        
        public void Sort()
        {
        #if COMPILER_UDONSHARP
            T[] itemArr = _items;
            int size = _size;

            if (size < 16) // Insertion sort is faster for small collections
            {
                for (int i = 1; i < size; i++)
                {
                    T key = itemArr[i];
                    int j = i - 1;
            
                    while (j >= 0 && itemArr[j].CompareTo(key) > 0)
                    {
                        itemArr[j + 1] = itemArr[j];
                        j--;
                    }
            
                    itemArr[j + 1] = key;
                }
            }
            else // Iterative QuickSort for larger collections, iterative because recursion is very expensive in Udon
            {
                int[] stack = _sortStack;

                if (stack == null)
                {
                    // 32 is the maximum stack depth for a collection of 2^32 elements, so 64 since we're using 2 ints per stack frame
                    // Of course Udon will time out long before that
                    _sortStack = stack = new int[64]; 
                }

                int stackSize = 0;

                stack[stackSize++] = 0;
                stack[stackSize++] = size - 1;

                while (stackSize > 0)
                {
                    int end = stack[--stackSize];
                    int start = stack[--stackSize];

                    if (end - start < 16)
                    {
                        for (int i = start + 1; i <= end; i++)
                        {
                            T key = itemArr[i];
                            int j = i - 1;
                    
                            while (j >= start && itemArr[j].CompareTo(key) > 0)
                            {
                                itemArr[j + 1] = itemArr[j];
                                j--;
                            }
                    
                            itemArr[j + 1] = key;
                        }
                    
                        continue;
                    }

                    int pivotIndex = (start + end) / 2;
                    T pivot = itemArr[pivotIndex];

                    itemArr[pivotIndex] = itemArr[end];
                    itemArr[end] = pivot;

                    int storeIndex = start;

                    for (int i = start; i < end; i++)
                    {
                        if (itemArr[i].CompareTo(pivot) <= 0)
                        {
                            T temp = itemArr[i];
                            itemArr[i] = itemArr[storeIndex];
                            itemArr[storeIndex] = temp;
                            storeIndex++;
                        }
                    }

                    itemArr[end] = itemArr[storeIndex];
                    itemArr[storeIndex] = pivot;

                    if (storeIndex - start > 1)
                    {
                        stack[stackSize++] = start;
                        stack[stackSize++] = storeIndex - 1;
                    }

                    if (end - storeIndex > 1)
                    {
                        stack[stackSize++] = storeIndex + 1;
                        stack[stackSize++] = end;
                    }
                }
            }
            #endif
        }
        
        public void Clear()
        {
            _size = 0;
            _items = new T[4];
        }
        
        public T[] ToArray()
        {
            T[] itemArr = new T[_size];
            System.Array.Copy(_items, itemArr, _size);
            return itemArr;
        }
        
        public bool Remove(T item)
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (item == null)
            {
                for (int i = 0; i < size; i++)
                {
                    if (itemArr[i] == null)
                    {
                        RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }

            for (int i = 0; i < size; i++)
            {
                if (item.Equals(itemArr[i]))
                {
                    RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
        
        public void RemoveAt(int index)
        {
            // Cache these because they aren't trivial to access in Udon
            int size = _size;
            T[] itemArr = _items;
            
            if (index < 0 || index >= size)
            {
                Debug.LogError($"Index out of range: {index}");
            #pragma warning disable CS0251
                itemArr[-1] = itemArr[0]; // throw new System.IndexOutOfRangeException();
            #pragma warning restore CS0251
                return;
            }
            
            for (int i = index; i < size - 1; i++)
            {
                itemArr[i] = itemArr[i + 1];
            }
            
            itemArr[size - 1] = default(T);
            
            _size = size - 1;
        }
        
        public bool Contains(T item)
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (item == null)
            {
                for (int i = 0; i < size; i++)
                {
                    if (itemArr[i] == null)
                        return true;
                }

                return false;
            }

            for (int i = 0; i < size; i++)
            {
                if (item.Equals(itemArr[i]))
                    return true;
            }

            return false;
        }
        
        public void Insert(int index, T item)
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (index < 0 || index > size)
            {
                Debug.LogError($"Index out of range: {index}");
            #pragma warning disable CS0251
                itemArr[-1] = itemArr[0]; // throw new System.IndexOutOfRangeException();
            #pragma warning restore CS0251
                return;
            }
            
            if (size == itemArr.Length)
            {
                T[] newItems = new T[itemArr.Length * 2];
                System.Array.Copy(itemArr, newItems, itemArr.Length);
                _items = newItems;
                itemArr = newItems;
            }
            
            for (int i = size; i > index; i--)
            {
                itemArr[i] = itemArr[i - 1];
            }
            
            itemArr[index] = item;
            _size = size + 1;
        }
        
        public int IndexOf(T item)
        {
            int size = _size;
            T[] itemArr = _items;
            
            if (item == null)
            {
                for (int i = 0; i < size; i++)
                {
                    if (itemArr[i] == null)
                        return i;
                }
            
                return -1;
            }
            
            for (int i = 0; i < size; i++)
            {
                if (item.Equals(itemArr[i]))
                    return i;
            }
            
            return -1;
            
            // return Array.IndexOf(_items, (object)item, 0, _size);
        }
        
        public int IndexOfFast(T item)
        {
            return Array.IndexOf(_items, (object)item, 0, _size);
        }
        
        public void Reverse()
        {
            Array.Reverse(_items, 0, _size);
        }
        
        public int Count => _size;
        
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _size)
                {
                    Debug.LogError($"Index out of range: {index}");
                #pragma warning disable CS0251
                    return _items[-1]; // throw new System.IndexOutOfRangeException();
                #pragma warning restore CS0251
                }
                
                return _items[index];
            }
            set
            {
                if (index < 0 || index >= _size)
                {
                    Debug.LogError($"Index out of range: {index}");
                #pragma warning disable CS0251
                    _items[-1] = value; // throw new System.IndexOutOfRangeException();
                #pragma warning restore CS0251
                    return;
                }
                
                _items[index] = value;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return new ListIterator<T>(this);
        }
    }
}