﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using NetUtilities;

namespace System.Reflection
{
    /// <inheritdoc/>
    public class ConstructorData : MemberData<ConstructorInfo>
    {
        private readonly Type _target;
        private readonly Lazy<Func<object[], object>?> _constructor;

        /// <summary>
        ///     Gets the parameters of this constructor.
        /// </summary>
        public ReadOnlyList<ParameterInfo> Parameters { get; }

        /// <summary>
        ///     Indicates if this constructor is the default constructor.
        /// </summary>
        public bool IsDefault { get; }

        /// <summary>
        ///     Initializes a new instance of <see cref="ConstructorData"/> class 
        ///     with the provided <see cref="ConstructorInfo"/> and target.
        /// </summary>
        /// <param name="constructor">
        ///     The constructor.
        /// </param>
        /// <param name="target">
        ///     The target.
        /// </param>
        public ConstructorData(ConstructorInfo constructor, Type target) : base(constructor)
        {
            var @params = constructor.GetParameters();

            _target = target;
            _constructor = new(() => 
            {
                var array = Expression.Parameter(typeof(object[]));
                var parameters = @params.Select((arg, index) => Expression.Convert(
                    Expression.ArrayIndex(
                        array,
                        Expression.Constant(index)),
                    arg.GetType())).ToArray();
                var @new = Expression.New(Member, parameters);
                var convert = Expression.Convert(@new, typeof(object));

                return Expression.Lambda<Func<object[], object>>(convert, array).Compile();
            }, true);

            Parameters = @params.ToReadOnlyList();
            IsDefault = target.IsValueType || Parameters.Count == 0;
        }

        /// <summary>
        ///     Creates an instance of the type this constructor belongs to.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Throw if this constructors requires parameters 
        ///     -- OR -- 
        ///     the type this constructor belongs to is either <see langword="abstract"/> or <see langword="static"/>.
        /// </exception>
        /// <returns>
        ///     An instance of the type this constructor belongs to.
        /// </returns>
        public object CreateInstance()
        {
            if (_target.IsAbstract)
                throw new InvalidOperationException(
                    $"You cannot create an instance of an {(_target.IsSealed ? "static" : "abstract")} type.");

            if (Parameters.Count > 0)
                throw new InvalidOperationException(
                    $"This constructor {_target.Name}({string.Join(", ", Parameters.Select(x => x.ParameterType.Name))}) requires these parameters to be used.");

            return Factory.CreateInstance(_target);
        }

        /// <summary>
        ///     Creates an instance of the type this constructor belongs to with the provided arguments.
        /// </summary>
        /// <param name="args">
        ///     The arguments.
        /// </param>
        /// <exception cref="ArgumentException">
        ///     Thrown when the supplied arguments count is different from the parameters count of this constructor.
        /// </exception>
        /// <exception cref="InvalidCastException">
        ///     Thrown when one of the arguments couldn't be casted to the respective type.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the type this constructor belongs to is either <see langword="abstract"/> or <see langword="static"/>.
        /// </exception>
        /// <returns>
        ///     An instance of the type this constructor belongs to.
        /// </returns>
        public object CreateInstance(params object[] args)
        {
            if (args is null || args.Length == 0)
                return CreateInstance();

            if (_target.IsAbstract)
                throw new InvalidOperationException(
                    $"You cannot create an instance of an {(_target.IsSealed ? "static" : "abstract")} type.");

            if (Parameters.Count != args.Length)
                throw new InvalidOperationException(
                    $"This constructor {_target.Name}({string.Join(", ", Parameters.Select(x => x.ParameterType.Name))}) requires these parameters to be used.");

            return _constructor.Value(args);
        }
    }
}
