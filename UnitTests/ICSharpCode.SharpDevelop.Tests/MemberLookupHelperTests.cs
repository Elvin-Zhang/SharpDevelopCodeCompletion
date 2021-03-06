﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System.Collections.Generic;
using System.Windows.Forms;
using ICSharpCode.SharpDevelop.Dom;
using NUnit.Framework;

namespace ICSharpCode.SharpDevelop.Tests
{
    [TestFixture]
    public class MemberLookupHelperTests
    {
        private IProjectContent msc; // = ProjectContentRegistry.Mscorlib;
        private IProjectContent swf; // = ProjectContentRegistry.GetProjectContentForReference("System.Windows.Forms", "System.Windows.Forms");
        private DefaultClass dummyClass;
        private IMethod methodForGenericCalls;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            var r = new ProjectContentRegistry();
            msc = r.Mscorlib;
            swf = r.GetProjectContentForReference("System.Windows.Forms", typeof (Form).Module.FullyQualifiedName);

            var dpc = new DefaultProjectContent();
            dpc.ReferencedContents.Add(msc);
            var cu = new DefaultCompilationUnit(dpc);
            dummyClass = new DefaultClass(cu, "DummyClass");
            cu.Classes.Add(dummyClass);
            methodForGenericCalls = new DefaultMethod(dummyClass, "DummyMethod");
            dummyClass.Methods.Add(methodForGenericCalls);
        }

        private IReturnType DictionaryRT
        {
            get { return new GetClassReturnType(msc, "System.Collections.Generic.Dictionary", 2); }
        }

        private IClass EnumerableClass
        {
            get { return msc.GetClass("System.Collections.Generic.IEnumerable", 1); }
        }

        private ConstructedReturnType EnumerableOf(IReturnType element)
        {
            return new ConstructedReturnType(EnumerableClass.DefaultReturnType, new[] {element});
        }

        private ConstructedReturnType IListOf(IReturnType element)
        {
            return new ConstructedReturnType(msc.GetClass("System.Collections.Generic.IList", 1).DefaultReturnType, new[] {element});
        }

        private ConstructedReturnType ListOf(IReturnType element)
        {
            return new ConstructedReturnType(msc.GetClass("System.Collections.Generic.List", 1).DefaultReturnType, new[] {element});
        }

        private DefaultClass CreateClassDerivingFromListOfString()
        {
            var dpc = new DefaultProjectContent();
            dpc.ReferencedContents.Add(msc);
            var cu = new DefaultCompilationUnit(dpc);
            cu.UsingScope.Usings.Add(new DefaultUsing(dpc, new DomRegion(1, 1, 5, 5)));
            cu.UsingScope.Usings[0].Usings.Add("System.Collections.Generic");

            var listDerivingClass = new DefaultClass(cu, "DerivesFromList");
            cu.Classes.Add(listDerivingClass);
            listDerivingClass.BaseTypes.Add(new ConstructedReturnType(new SearchClassReturnType(dpc, listDerivingClass, 3, 1,
                "List", 1),
                new IReturnType[]
                    {
                        new GetClassReturnType(dpc, "System.String", 0)
                    }));

            return listDerivingClass;
        }

        private DefaultClass CreateGenericClassDerivingFromList()
        {
            var dpc = new DefaultProjectContent();
            dpc.ReferencedContents.Add(msc);
            var cu = new DefaultCompilationUnit(dpc);
            cu.UsingScope.Usings.Add(new DefaultUsing(dpc, new DomRegion(1, 1, 5, 5)));
            cu.UsingScope.Usings[0].Usings.Add("System.Collections.Generic");

            var listDerivingClass = new DefaultClass(cu, "DerivesFromList");
            cu.Classes.Add(listDerivingClass);
            listDerivingClass.TypeParameters.Add(new DefaultTypeParameter(listDerivingClass, "T", 0));
            listDerivingClass.BaseTypes.Add(new ConstructedReturnType(new SearchClassReturnType(dpc, listDerivingClass, 3, 1,
                "List", 1),
                new IReturnType[]
                    {
                        new GenericReturnType(listDerivingClass.TypeParameters[0])
                    }));
            return listDerivingClass;
        }

        private bool IsApplicable(IReturnType argument, IReturnType expected)
        {
            return MemberLookupHelper.IsApplicable(argument, expected, methodForGenericCalls);
        }

        private GenericReturnType CreateT()
        {
            ITypeParameter tp = new DefaultTypeParameter(methodForGenericCalls, "T", 0);
            return new GenericReturnType(tp);
        }

        private GenericReturnType CreateTWithDisposableConstraint()
        {
            GenericReturnType rt = CreateT();
            rt.TypeParameter.Constraints.Add(msc.GetClass("System.IDisposable", 0).DefaultReturnType);
            return rt;
        }

        [Test]
        public void ArrayOfStringIsApplicableOnArrayOfT()
        {
            Assert.IsTrue(IsApplicable(new ArrayReturnType(msc, msc.SystemTypes.String, 1),
                new ArrayReturnType(msc, CreateT(), 1)));

            Assert.IsFalse(MemberLookupHelper.ConversionExists(new ArrayReturnType(msc, msc.SystemTypes.String, 1),
                new ArrayReturnType(msc, CreateT(), 1)));
        }

        [Test]
        public void ArrayOfStringIsApplicableOnIListOfT()
        {
            Assert.IsTrue(IsApplicable(new ArrayReturnType(msc, msc.SystemTypes.String, 1),
                IListOf(CreateT())));

            Assert.IsFalse(MemberLookupHelper.ConversionExists(new ArrayReturnType(msc, msc.SystemTypes.String, 1),
                IListOf(CreateT())));
        }

        [Test]
        public void ConversionDoesNotExistFromAnonymousDelegateWithParameterToSystemPredicateWhenParameterTypeIsIncompatible()
        {
            var amrt = new AnonymousMethodReturnType(new DefaultCompilationUnit(msc));
            amrt.MethodReturnType = msc.SystemTypes.Boolean;
            amrt.MethodParameters = new List<IParameter>();
            amrt.MethodParameters.Add(new DefaultParameter("test", msc.SystemTypes.String, DomRegion.Empty));
            Assert.IsFalse(MemberLookupHelper.ConversionExists(
                amrt,
                new ConstructedReturnType(new GetClassReturnType(msc, "System.Predicate", 1),
                    new[] {msc.SystemTypes.Int32})
                ));
        }

        [Test]
        public void ConversionExistsFromAnonymousDelegateToSystemPredicate()
        {
            Assert.IsTrue(IsApplicable(
                new AnonymousMethodReturnType(new DefaultCompilationUnit(msc)) {MethodReturnType = msc.SystemTypes.Boolean},
                new GetClassReturnType(msc, "System.Predicate", 1)
                ));
        }

        [Test]
        public void ConversionExistsFromAnonymousDelegateWithParameterToSystemPredicate()
        {
            var amrt = new AnonymousMethodReturnType(new DefaultCompilationUnit(msc));
            amrt.MethodReturnType = msc.SystemTypes.Boolean;
            amrt.MethodParameters = new List<IParameter>();
            amrt.MethodParameters.Add(new DefaultParameter("test", msc.SystemTypes.String, DomRegion.Empty));
            Assert.IsTrue(MemberLookupHelper.ConversionExists(
                amrt,
                new ConstructedReturnType(new GetClassReturnType(msc, "System.Predicate", 1),
                    new[] {msc.SystemTypes.String})
                ));
        }

        [Test]
        public void ConversionExistsFromClassDerivingFromListOfStringToListOfString()
        {
            Assert.IsTrue(MemberLookupHelper.ConversionExists(CreateClassDerivingFromListOfString().DefaultReturnType,
                ListOf(msc.SystemTypes.String)));
        }

        [Test]
        public void ConversionExistsFromClassDerivingFromListOfStringToStringEnumerable()
        {
            Assert.IsTrue(MemberLookupHelper.ConversionExists(CreateClassDerivingFromListOfString().DefaultReturnType,
                EnumerableOf(msc.SystemTypes.String)));
        }

        [Test]
        public void ConversionExistsFromStringArrayToObjectArray()
        {
            Assert.IsTrue(MemberLookupHelper.ConversionExists(new ArrayReturnType(msc, msc.SystemTypes.String, 1),
                new ArrayReturnType(msc, msc.SystemTypes.Object, 1)));
        }

        [Test]
        public void ConversionExistsFromStringArrayToStringEnumerable()
        {
            Assert.IsTrue(MemberLookupHelper.ConversionExists(new ArrayReturnType(msc, msc.SystemTypes.String, 1),
                EnumerableOf(msc.SystemTypes.String)));
        }

        [Test]
        public void ConversionExistsFromStringIListToStringEnumerable()
        {
            Assert.IsTrue(MemberLookupHelper.ConversionExists(IListOf(msc.SystemTypes.String),
                EnumerableOf(msc.SystemTypes.String)));
        }

        [Test]
        public void ConversionExistsFromStringListToStringEnumerable()
        {
            Assert.IsTrue(MemberLookupHelper.ConversionExists(ListOf(msc.SystemTypes.String),
                EnumerableOf(msc.SystemTypes.String)));
        }

        [Test]
        public void DisposableClassIsApplicableOnDisposableT()
        {
            Assert.IsFalse(MemberLookupHelper.ConversionExists(msc.GetClass("System.CharEnumerator", 0).DefaultReturnType,
                CreateTWithDisposableConstraint()));

            Assert.IsTrue(IsApplicable(msc.GetClass("System.CharEnumerator", 0).DefaultReturnType,
                CreateTWithDisposableConstraint()));
        }

        [Test]
        public void GetCommonType()
        {
            IReturnType res = MemberLookupHelper.GetCommonType(msc,
                swf.GetClass("System.Windows.Forms.ToolStripButton", 0).DefaultReturnType,
                swf.GetClass("System.Windows.Forms.ToolStripSeparator", 0).DefaultReturnType);
            Assert.AreEqual("System.Windows.Forms.ToolStripItem", res.FullyQualifiedName);
        }

        [Test]
        public void GetCommonTypeOfNullAndString()
        {
            IReturnType res = MemberLookupHelper.GetCommonType(msc,
                NullReturnType.Instance,
                msc.GetClass("System.String", 0).DefaultReturnType);
            Assert.AreEqual("System.String", res.FullyQualifiedName);
        }

        [Test]
        public void GetCommonTypeOfStringAndNull()
        {
            IReturnType res = MemberLookupHelper.GetCommonType(msc,
                msc.GetClass("System.String", 0).DefaultReturnType,
                NullReturnType.Instance);
            Assert.AreEqual("System.String", res.FullyQualifiedName);
        }

        [Test]
        public void GetTypeInheritanceTreeOfClassDerivingFromListOfString()
        {
            var results = new List<IReturnType>(
                MemberLookupHelper.GetTypeInheritanceTree(CreateClassDerivingFromListOfString().DefaultReturnType)
                ).ConvertAll(rt => rt.DotNetName);

            results.Sort(); // order is not guaranteed, so sort for the unit test

            Assert.AreEqual("DerivesFromList;" +
                "System.Collections.Generic.ICollection{System.String};" +
                    "System.Collections.Generic.IEnumerable{System.String};" +
                        "System.Collections.Generic.IList{System.String};" +
                            "System.Collections.Generic.IReadOnlyCollection{System.String};" +
                                "System.Collections.Generic.IReadOnlyList{System.String};" +
                                    "System.Collections.Generic.List{System.String};" +
                                        "System.Collections.ICollection;" +
                                            "System.Collections.IEnumerable;" +
                                                "System.Collections.IList;" +
                                                    "System.Object",
                string.Join(";", results.ToArray()));
        }

        [Test]
        public void GetTypeInheritanceTreeOfStringArray()
        {
            List<string> results = new List<IReturnType>(
                MemberLookupHelper.GetTypeInheritanceTree(new ArrayReturnType(msc, msc.SystemTypes.String, 1))
                ).ConvertAll(delegate(IReturnType rt) { return rt.DotNetName; });

            results.Sort(); // order is not guaranteed, so sort for the unit test

            Assert.AreEqual("System.Collections.Generic.ICollection{System.String};" +
                "System.Collections.Generic.IEnumerable{System.String};" +
                    "System.Collections.Generic.IList{System.String};" +
                        "System.Collections.ICollection;" +
                            "System.Collections.IEnumerable;" +
                                "System.Collections.IList;" +
                                    "System.Object;" +
                                        "System.String[]",
                string.Join(";", results.ToArray()));
        }

        [Test]
        public void ListOfStringIsApplicableOnIEnumerableOfT()
        {
            Assert.IsTrue(IsApplicable(ListOf(msc.SystemTypes.String),
                EnumerableOf(CreateT())));
        }

        [Test]
        public void ListOfStringIsApplicableOnListOfT()
        {
            Assert.IsTrue(IsApplicable(ListOf(msc.SystemTypes.String),
                ListOf(CreateT())));
        }

        [Test]
        public void LocalVariableAndFieldAreNotSimilarMembers()
        {
            IField field = new DefaultField(dummyClass.DefaultReturnType, "Test", ModifierEnum.None, DomRegion.Empty, dummyClass);
            IField local = new DefaultField.LocalVariableField(dummyClass.DefaultReturnType, "Test", DomRegion.Empty, dummyClass);
            Assert.IsFalse(MemberLookupHelper.IsSimilarMember(local, field));
        }

        [Test]
        public void NoConversionExistsFromObjectArrayToStringArray()
        {
            Assert.IsFalse(MemberLookupHelper.ConversionExists(new ArrayReturnType(msc, msc.SystemTypes.Object, 1),
                new ArrayReturnType(msc, msc.SystemTypes.String, 1)));
        }

        [Test]
        public void NoConversionExistsFromParameterlessAnonymousDelegateToSystemPredicate()
        {
            var amrt = new AnonymousMethodReturnType(new DefaultCompilationUnit(msc));
            amrt.MethodParameters = new List<IParameter>();
            Assert.IsFalse(MemberLookupHelper.ConversionExists(
                amrt,
                new GetClassReturnType(msc, "System.Predicate", 1)
                ));
        }

        [Test]
        public void NoConversionExistsFromStringEnumerableToObjectEnumerable()
        {
            Assert.IsFalse(MemberLookupHelper.ConversionExists(EnumerableOf(msc.SystemTypes.String),
                EnumerableOf(msc.SystemTypes.Object)));
        }

        [Test]
        public void NoConversionExistsFromStringIListToIntEnumerable()
        {
            Assert.IsFalse(MemberLookupHelper.ConversionExists(IListOf(msc.SystemTypes.String),
                EnumerableOf(msc.SystemTypes.Int32)));
        }

        [Test]
        public void NoConversionExistsFromStringToDisposableT()
        {
            // no conversion exists
            Assert.IsFalse(MemberLookupHelper.ConversionExists(msc.SystemTypes.String,
                CreateTWithDisposableConstraint()));

            // but it is applicable (applicability ignores constraints)
            Assert.IsTrue(IsApplicable(msc.SystemTypes.String,
                CreateTWithDisposableConstraint()));
        }

        [Test]
        public void StringIsApplicableOnT()
        {
            // no conversion exists
            Assert.IsFalse(MemberLookupHelper.ConversionExists(msc.SystemTypes.String,
                CreateT()));

            // but it is applicable
            Assert.IsTrue(IsApplicable(msc.SystemTypes.String,
                CreateT()));
        }

        [Test]
        public void TypeParameterPassedToBaseClassSameClass()
        {
            IReturnType[] stringArr = {msc.SystemTypes.String};
            IReturnType rrt = new ConstructedReturnType(EnumerableClass.DefaultReturnType, stringArr);
            IReturnType res = MemberLookupHelper.GetTypeParameterPassedToBaseClass(rrt, EnumerableClass, 0);
            Assert.AreEqual("System.String", res.FullyQualifiedName);
        }

        [Test]
        public void TypeParameterPassedToBaseClassTestClassDerivingFromList()
        {
            DefaultClass listDerivingClass = CreateClassDerivingFromListOfString();

            IReturnType res = MemberLookupHelper.GetTypeParameterPassedToBaseClass(listDerivingClass.DefaultReturnType,
                EnumerableClass, 0);
            Assert.AreEqual("System.String", res.FullyQualifiedName);
        }

        [Test]
        public void TypeParameterPassedToBaseClassTestDictionary()
        {
            IReturnType[] stringInt = {msc.SystemTypes.String, msc.SystemTypes.Int32};
            IReturnType rrt = new ConstructedReturnType(DictionaryRT, stringInt);
            IReturnType res = MemberLookupHelper.GetTypeParameterPassedToBaseClass(rrt, EnumerableClass, 0);
            Assert.AreEqual("System.Collections.Generic.KeyValuePair", res.FullyQualifiedName);
            ConstructedReturnType resc = res.CastToConstructedReturnType();
            Assert.AreEqual("System.String", resc.TypeArguments[0].FullyQualifiedName);
            Assert.AreEqual("System.Int32", resc.TypeArguments[1].FullyQualifiedName);
        }

        [Test]
        public void TypeParameterPassedToBaseClassTestGenericClassDerivingFromList()
        {
            DefaultClass listDerivingClass = CreateGenericClassDerivingFromList();

            var testType = new ConstructedReturnType(listDerivingClass.DefaultReturnType,
                new[] {msc.SystemTypes.String});

            IReturnType res = MemberLookupHelper.GetTypeParameterPassedToBaseClass(testType,
                EnumerableClass, 0);
            Assert.AreEqual("System.String", res.FullyQualifiedName);
        }

        [Test]
        public void TypeParameterPassedToBaseClassTestString()
        {
            IReturnType res = MemberLookupHelper.GetTypeParameterPassedToBaseClass(msc.SystemTypes.String, EnumerableClass, 0);
            Assert.AreEqual("System.Char", res.FullyQualifiedName);
        }
    }
}