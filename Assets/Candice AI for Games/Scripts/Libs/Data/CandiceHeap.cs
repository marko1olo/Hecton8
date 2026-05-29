using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CandiceAIforGames.data
{
    public class CandiceHeap<T> where T : ICandiceHeapItem<T>
    {
        T[] items;
        int currentItemCount;

        public CandiceHeap(int maxHeapSize)
        {
            items = new T[maxHeapSize];
        }
        public void Add(T item)
        {
            item.HeapIndex = currentItemCount;
            items[currentItemCount] = item;
            SortUp(item);
            currentItemCount++;
        }
        public void Clear()
        {
            currentItemCount = 0;
        }
        public T RemoveFirst()
        {
            T firstItem = items[0];
            currentItemCount--;
            if (currentItemCount > 0)
            {
                items[0] = items[currentItemCount];
                items[0].HeapIndex = 0;
                items[currentItemCount] = default(T);
                SortDown(items[0]);
            }
            else
            {
                items[0] = default(T);
            }
            return firstItem;
        }
        void SortDown(T item)
        {
            while (true)
            {
                int childIndexLeft = item.HeapIndex * 2 + 1;
                int childIndexRight = item.HeapIndex * 2 + 2;
                int swapIndex = 0;
                if (childIndexLeft < currentItemCount)
                {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < currentItemCount)
                    {
                        if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0)
                        {
                            swapIndex = childIndexRight;
                        }
                    }
                    if (item.CompareTo(items[swapIndex]) < 0)
                    {
                        Swap(item, items[swapIndex]);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }
        public void UpdateItem(T item)
        {
            SortUp(item);
        }
        public int Count
        {
            get { return currentItemCount; }
        }
        public bool Contains(T item)
        {
            int heapIndex = item.HeapIndex;
            return heapIndex >= 0 && heapIndex < currentItemCount && Equals(items[heapIndex], item);
        }
        void SortUp(T item)
        {
            int parentIndex = (item.HeapIndex - 1) / 2;
            while (true)
            {
                T parentItem = items[parentIndex];
                if (item.CompareTo(parentItem) > 0)
                {
                    Swap(item, parentItem);
                }
                else
                {
                    return;
                }
                parentIndex = (item.HeapIndex - 1) / 2;
            }
        }
        void Swap(T itemA, T itemB)
        {
            items[itemA.HeapIndex] = itemB;
            items[itemB.HeapIndex] = itemA;
            int itemAIndex = itemA.HeapIndex;
            itemA.HeapIndex = itemB.HeapIndex;
            itemB.HeapIndex = itemAIndex;
        }
    }
    public interface ICandiceHeapItem<T> : IComparable<T>
    {
        int HeapIndex
        {
            get;
            set;
        }
    }
}
