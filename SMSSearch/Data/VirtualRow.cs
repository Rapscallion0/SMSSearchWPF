using System;
using System.ComponentModel;

namespace SMS_Search.Data
{
    public class VirtualRow : CustomTypeDescriptor, IEquatable<VirtualRow>
    {
        private readonly VirtualGridContext _context;
        private readonly int _rowIndex;
        private readonly PropertyDescriptorCollection _properties;

        public int RowIndex => _rowIndex;

        // Expose Context internal for equality check or just use in Equals
        internal VirtualGridContext Context => _context;

        public VirtualRow(VirtualGridContext context, int rowIndex, PropertyDescriptorCollection properties)
        {
            _context = context;
            _rowIndex = rowIndex;
            _properties = properties;
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return _properties;
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
        {
            return _properties;
        }

        public object? GetValue(int colIndex)
        {
            return _context.GetValue(_rowIndex, colIndex);
        }

        public bool Equals(VirtualRow? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(_context, other._context) && _rowIndex == other._rowIndex;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null) return false;
            return Equals(obj as VirtualRow);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_context, _rowIndex);
        }
    }

    public class VirtualPropertyDescriptor : PropertyDescriptor
    {
        private readonly int _colIndex;
        private readonly Type _colType;

        public VirtualPropertyDescriptor(string name, int colIndex, Type colType)
            : base(name, null)
        {
            _colIndex = colIndex;
            _colType = colType;
        }

        public override Type ComponentType => typeof(VirtualRow);
        public override bool IsReadOnly => true;
        public override Type PropertyType => _colType;
        public override bool CanResetValue(object component) => false;
        public override object? GetValue(object? component)
        {
            if (component is VirtualRow row)
            {
                return row.GetValue(_colIndex);
            }
            return null;
        }
        public override void ResetValue(object component) { }
        public override void SetValue(object? component, object? value) { }
        public override bool ShouldSerializeValue(object component) => false;
    }
}
