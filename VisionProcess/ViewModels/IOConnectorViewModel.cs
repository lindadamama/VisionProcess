﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using VisionProcess.Core.Attributes;
using VisionProcess.Models;

namespace VisionProcess.ViewModels
{
    [Flags]
    public enum ValueStatus
    {
        None = 0,
        CanRead = 1,
        CanWrite = 2,
        All = CanRead | CanWrite
    }

    public class TreeNode
    {
        public TreeNode(string path, object? value, Type type, ValueStatus state, TreeNode? parent)
        {
            Path = path;
            State = state;
            Type = type;
            Value = value;
            if (parent == null)
            {
                FullPath = path;
            }
            else
            {
                FullPath = path.StartsWith('[') ? parent.FullPath + path : parent.FullPath + "." + path;
            }
        }
        public ObservableCollection<TreeNode> ChildNodes { get; } = [];
        public string FullPath { get; }
        public string Path { get; }
        public ValueStatus State { get; }
        public Type Type { get; }
        public object? Value { get; }
    }

    internal partial class IOConnectorViewModel : ObservableObject
    {
        private readonly OperationModel operationModel;

        private readonly HashSet<object?>? visited = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddInputCommand), nameof(AddOutputCommand))]
        private TreeNode? selectedNode;

        [ObservableProperty]
        private ObservableCollection<TreeNode> treeNodes = [];

        public IOConnectorViewModel(OperationModel operationModel)
        {
            this.operationModel = operationModel;

            if (operationModel.Operator is not null)
                FetchPropertyAndMethodInfo(operationModel.Operator, TreeNodes, null);
            visited = null;
        }

        [RelayCommand(CanExecute = nameof(CanAddInput))]
        private void AddInput()
        {
            if (SelectedNode == null) return;
            operationModel.Inputs.Add(
                new ConnectorModel(SelectedNode.FullPath.Split('.')[^1], SelectedNode.Type,
                SelectedNode.FullPath, true, operationModel.Id, operationModel));
            AddInputCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanAddOutput))]
        private void AddOutput()
        {
            if (SelectedNode == null) return;
            operationModel.Outputs.Add(
                new ConnectorModel(SelectedNode.FullPath.Split('.')[^1], SelectedNode.Type,
                SelectedNode.FullPath, false, operationModel.Id, operationModel));
            AddOutputCommand.NotifyCanExecuteChanged();
        }

        private void AssignItemToTreeNode(object? instance, string propertyName, Type type, ValueStatus state, ObservableCollection<TreeNode> treeNodes, int index, TreeNode? parent)
        {
            string fullPath = propertyName + $"[{index}]";
            var newTreeNode = new TreeNode(fullPath, instance, type, state, parent);
            treeNodes.Add(newTreeNode);
            //获取当前的
            FetchPropertyAndMethodInfo(instance, treeNodes[^1].ChildNodes, newTreeNode);
        }

        private void AssignPropertyTreeNode(object? instance, PropertyInfo propertyInfo, ObservableCollection<TreeNode> treeNodes, TreeNode? parent)
        {
            var newTreeNode = AssignTreeNodeByPropertyInfo(instance, propertyInfo, treeNodes, parent);
            if (newTreeNode is null)
                return;
            //获取当前的
            FetchPropertyAndMethodInfo(instance, treeNodes[^1].ChildNodes, newTreeNode);
        }

        private TreeNode? AssignTreeNodeByPropertyInfo(object? instance, PropertyInfo propertyInfo, ObservableCollection<TreeNode> treeNodes, TreeNode? parent)
        {
            ValueStatus state = ValueStatus.None;
            if (propertyInfo.GetMethod is null || propertyInfo.GetMethod.IsPublic && !propertyInfo.GetMethod.IsStatic)
                state |= ValueStatus.CanRead;
            if (propertyInfo.SetMethod is not null && propertyInfo.SetMethod.IsPublic)
                state |= ValueStatus.CanWrite;
            if (state == ValueStatus.None)
                return null;

            var newTreeNode = new TreeNode(propertyInfo.Name, instance, propertyInfo.PropertyType, state, parent);
            treeNodes.Add(newTreeNode);
            return newTreeNode;
        }

        private bool CanAddInput()
        {
            if (SelectedNode is null)
                return false;
            return operationModel.Inputs.FirstOrDefault(x => x.ValuePath == SelectedNode.FullPath) == null &&
                         (SelectedNode.State & ValueStatus.CanWrite) == ValueStatus.CanWrite &&
                         !SelectedNode.FullPath.Contains('(');//如果是方法获得的就不能当作输入
        }

        private bool CanAddOutput()
        {
            if (SelectedNode is null)
                return false;
            return operationModel.Outputs.FirstOrDefault(x => x.ValuePath == SelectedNode.FullPath) == null &&
                         (SelectedNode.State & ValueStatus.CanRead) == ValueStatus.CanRead;
        }

        /// <summary>
        /// 特殊处理类型为IList Array 的实例、,且将搜索到的实例再扔进<see cref="FetchPropertyAndMethodInfo"/>递归搜索更多
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="treeNodes"></param>
        /// <param name="parent"></param>
        private void FetchArrayOrIListPropertyAndMethodInfo(object instance, PropertyInfo propertyInfo, ObservableCollection<TreeNode> treeNodes, TreeNode? parent)
        {
            //复杂度较高 16 但是算了、、、
            //1、这里先将整个  加入TreeNode中先、
            var propertyInstance = propertyInfo.GetValue(instance);
            var newTreeNode = AssignTreeNodeByPropertyInfo(propertyInstance, propertyInfo, treeNodes, parent);

            if (newTreeNode is null)
                return;
            if (propertyInstance == null)
                return;
            //2、再获取IList Array 的所有属性、Length Count等、
            //Ilist的Item需要去掉Array中没有Item无妨
            var iListInstancePropertyInfos = propertyInfo.PropertyType.GetProperties()
                                 .Where(x => x.GetMethod != null && x.Name != "Item" && x.GetMethod.IsPublic && !x.GetMethod.IsStatic);
            foreach (var iListPropertyInfo in iListInstancePropertyInfos)
            {
                var iListPropertyInstance = iListPropertyInfo.GetValue(propertyInstance);
                AssignPropertyTreeNode(iListPropertyInstance, iListPropertyInfo, treeNodes[^1].ChildNodes, newTreeNode);
            }
            //3、再将所有 item 加入
            PropertyInfo? countInfo;
            Array? array = null;
            if (propertyInstance is Array a)//
            {
                array = a;
                countInfo = propertyInfo.PropertyType.GetProperty("Length");
            }
            else
            {
                countInfo = propertyInfo.PropertyType.GetProperty("Count");
            }
            if (countInfo == null || countInfo.GetValue(propertyInstance) is not int count)
                return;
            for (int i = 0; i < count; i++)
            {
                object? item;
                Type? type;
                if (array is not null)
                {
                    item = array.GetValue(i);
                    type = propertyInfo.PropertyType.GetElementType();
                }
                else
                {
                    PropertyInfo? itemPropertyInfo = propertyInstance.GetType().GetProperty("Item");
                    if (itemPropertyInfo == null)
                        return;
                    item = itemPropertyInfo.GetValue(propertyInstance, [i]);
                    type = itemPropertyInfo.PropertyType;
                }
                if (type is null)
                    return;
                AssignItemToTreeNode(item, string.Empty, type, ValueStatus.All, treeNodes[^1].ChildNodes, i, newTreeNode);
            }
        }

        /// <summary>
        /// 搜索实例的所有带返回值无参方法,且将搜索到的返回值再扔进<see cref="FetchPropertyAndMethodInfo"/>递归搜索更多
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="instanceType"></param>
        /// <param name="treeNodes"></param>
        /// <param name="parent"></param>
        private void FetchMethodInfo(object instance, Type instanceType, ObservableCollection<TreeNode> treeNodes, TreeNode? parent)
        {
            ////复杂度较高12，
            //////导致  System.StackOverflowException！！！！！需要修改！！！可能有些方法导致无限递归
            //MethodInfo[] methods = instanceType.GetMethods();
            //var targetMethods = methods.Where(x => x.IsPublic &&
            //                                                             !x.IsStatic &&
            //                                                              x.GetParameters().Length == 0 &&
            //                                                              x.ReturnType != typeof(void) &&
            //                                                              x.ReturnType.IsPublic &&
            //                                                             !x.ReturnType.IsPointer &&
            //                                                             !x.ReturnType.IsGenericParameter &&
            //                                                              x.ReturnType != typeof(MatExpr) &&
            //                                                             !x.IsSpecialName &&
            //                                                             !x.Name.Contains("Clone", StringComparison.OrdinalIgnoreCase) &&
            //                                                             !x.Name.Contains("Copy", StringComparison.OrdinalIgnoreCase));
            //foreach (var method in targetMethods)
            //{
            //    //System.InvalidOperationException:
            //    //“Late bound operations cannot be
            //    //performed on types or methods for
            //    //which ContainsGenericParameters is true.”

            //    object? returnValue = method.Invoke(instance, null);
            //    //if (returnValue is null || IsVisitedOrNoAllow(returnValue))
            //    //    continue;
            //    string path = method.Name + "()";
            //    treeNodes.Add(new TreeNode(path, returnValue, method.ReturnType, ValueStatus.CanRead, parent));
            //    FetchPropertyAndMethodInfo(returnValue, treeNodes[^1].ChildNodes, parent);
            //}
        }

        /// <summary>
        /// 搜索实例的所有属性和无参带返回值方法、且继续向下寻找知道没有更多为止、并添加到 <see cref="TreeNodes"/>
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="treeNodes"></param>
        /// <param name="parent"></param>
        private void FetchPropertyAndMethodInfo(object? instance, ObservableCollection<TreeNode> treeNodes, TreeNode? parent)
        {
            // 返回方法的还没完成！！！！！
            if (instance is null)
                return;

            #region 获取所有属性

            Type instanceType = instance.GetType();
            if (IsVisitedOrNoAllow(instance, instanceType))
                return;

            FetchPropertyInfo(instance, instanceType, treeNodes, parent);

            #endregion 获取所有属性

            #region 获取所有无参带返回值方法

            FetchMethodInfo(instance, instanceType, treeNodes, parent);

            #endregion 获取所有无参带返回值方法
        }

        /// <summary>
        /// 搜索实例的所有属性,且将搜索到的实例再扔进<see cref="FetchPropertyAndMethodInfo"/>递归搜索更多
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="instanceType"></param>
        /// <param name="treeNodes"></param>
        /// <param name="parent"></param>
        private void FetchPropertyInfo(object instance, Type instanceType, ObservableCollection<TreeNode> treeNodes, TreeNode? parent)
        {
            PropertyInfo[] propertyInfos = instanceType.GetProperties();
            if (propertyInfos.Length < 1)
                return;
            //GetMethod 必须为 Public；且不能为静态
            var targetPropertyInfos = propertyInfos.Where(x => x.GetMethod is not null &&
                                                                                x.GetMethod.IsPublic &&
                                                                                !x.GetMethod.IsStatic);
            foreach (var propertyInfo in targetPropertyInfos)
            {
                if (Attribute.GetCustomAttribute(propertyInfo, typeof(ThresholdIgnoreAttribute)) is not null ||
                    Attribute.GetCustomAttribute(propertyInfo, typeof(JsonPropertyAttribute)) is not null)
                    continue;
                if (propertyInfo.PropertyType.IsAssignableTo(typeof(IList)))
                {
                    FetchArrayOrIListPropertyAndMethodInfo(instance, propertyInfo, treeNodes, parent);
                }
                else if (propertyInfo.GetIndexParameters().Length > 0)//如果自定义类带引锁器,过滤。。。
                    continue;
                else//否则视为普通 object?
                {
                    var propertyInstance = propertyInfo.GetValue(instance);
                    AssignPropertyTreeNode(propertyInstance, propertyInfo, treeNodes, parent);
                }
            }
        }

        /// <summary>
        /// 判断实例是否搜索过防止无限递归、且判断是否允许该类型向下所搜
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool IsVisitedOrNoAllow(object instance, Type type)
        {
            bool isContains = visited!.Contains(instance);
            if (!isContains && type.IsClass)
                visited!.Add(instance);

            return isContains ||
                   instance is DisposableObject disposable && disposable.IsDisposed ||
                   type.IsPointer ||
                   type.IsNotPublic ||//若是不公开的类型都舍去
                   type == typeof(IntPtr) ||
                   type == typeof(UIntPtr) ||
                   type == typeof(DateTime) ||//DateTime、DateTimeOffset 中有个 Date 属性 导致无限递归
                   type == typeof(DateTimeOffset) ||
                   type.IsAssignableTo(typeof(IEnumerator));
        }

        [RelayCommand]
        private void TreeNodeSelected(TreeNode treeNode)
        {
            SelectedNode = treeNode;
        }
    }
}