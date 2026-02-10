using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;

namespace SMS_Search.Data
{
    public class VirtualizingCollection : IList, ITypedList, INotifyCollectionChanged
    {
        private readonly VirtualGridContext _context;
        private readonly PropertyDescriptorCollection _properties;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public VirtualizingCollection(VirtualGridContext context, DataTable schema)
        {
            _context = context;

            var props = new List<PropertyDescriptor>();
            for (int i = 0; i < schema.Columns.Count; i++)
            {
                props.Add(new VirtualPropertyDescriptor(schema.Columns[i].ColumnName, i, schema.Columns[i].DataType));
            }
            _properties = new PropertyDescriptorCollection(props.ToArray());
        }

        public void Refresh()
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public int Count => _context.TotalCount;

        public bool IsFixedSize => true;

        public bool IsReadOnly => true;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public object this[int index]
        {
            get => new VirtualRow(_context, index, _properties);
            set => throw new NotSupportedException();
        }

        public int Add(object value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object value) => false;
        public void CopyTo(Array array, int index) => throw new NotSupportedException();
        public IEnumerator GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }
        public int IndexOf(object value) => -1;
        public void Insert(int index, object value) => throw new NotSupportedException();
        public void Remove(object value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();

        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            return _properties;
        }

        public string GetListName(PropertyDescriptor[] listAccessors)
        {
            return "VirtualList";
        }
    }
}
