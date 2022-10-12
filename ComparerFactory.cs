﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CelSerEngine
{
    public static class ComparerFactory
    {
        public static IComparer CreateVectorComparer(ScanConstraint scanConstraint)
        {
            return scanConstraint.DataType.EnumType switch
            {
                EnumDataType.Short => new VectorComparer<short>(scanConstraint),
                EnumDataType.Integer => new VectorComparer<int>(scanConstraint),
                EnumDataType.Float => new VectorComparer<float>(scanConstraint),
                EnumDataType.Double => new VectorComparer<double>(scanConstraint),
                EnumDataType.Long => new VectorComparer<long>(scanConstraint),
                _ => new VectorComparer<int>(scanConstraint)
            };
        }

        public static IComparer CreateValueComparer(ScanConstraint scanConstraint)
        {
            return scanConstraint.DataType.EnumType switch
            {
                EnumDataType.Short => new ValueComparer<short>(scanConstraint),
                EnumDataType.Integer => new ValueComparer<int>(scanConstraint),
                EnumDataType.Float => new ValueComparer<float>(scanConstraint),
                EnumDataType.Double => new ValueComparer<double>(scanConstraint),
                EnumDataType.Long => new ValueComparer<long>(scanConstraint),
                _ => new ValueComparer<int>(scanConstraint)
            };
        }
    }
}
