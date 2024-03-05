﻿using OpenCvSharp;
using System.Collections;
using System.Reflection;

namespace VisionProcess.Core.Extensions
{
    public class PropertyMisc
    {
        /// <summary>
        /// 获取类对应属性名称的属性信息
        /// </summary>
        /// <param strings="instanceType">类型</param>
        /// <param strings="propertyName">属性名称</param>
        /// <returns></returns>
        public static PropertyInfo? GetPropertyInfo(Type type, string propertyName)
        {
            string[] array = propertyName.Split('[', ']');//防止引锁问题

            if (type.IsAssignableTo(typeof(IList)) && propertyName.Contains('['))
            {
                return type.GetProperty("Item");
            }
            return type.GetProperty(array.First());
        }

        public static Type? GetPropertyType(Type type, string propertyName)
        {
            string[] array = propertyName.Split('[', ']');//防止引锁问题
            if (propertyName.Contains('['))
            {
                if (type.IsAssignableTo(typeof(IList)))
                {
                    return type.GetProperty("Item")?.PropertyType;
                }
                if (type.IsArray)
                {
                    return type.GetElementType();
                }
            }
            return type.GetProperty(array.First())?.PropertyType;
        }
        /// <summary>
        /// 获取实例所对应的属性的值
        /// </summary>
        /// <param strings="instance">实例</param>
        /// <param strings="propertyName">属性名称</param>
        /// <returns></returns>
        public static object? GetPropertyValue(object? instance, string propertyName)
        {
            if (instance is null) return null;
            var instanceType = instance.GetType();
            PropertyInfo? propertyInfo = GetPropertyInfo(instanceType, propertyName);
            if (propertyInfo == null)
            {
                return null;
            }
            string[] strings = propertyName.Split('[', ']');
            if (!propertyName.Contains('['))
            {
                return propertyInfo.GetValue(instance);
            }
            if (!int.TryParse(strings[1], out int index))
            {
                return propertyInfo.GetValue(instance, [strings[1]]);
            }

            if (instance is Array array)
            {
                return array.GetValue(index);
            }
            return propertyInfo.GetValue(instance, [index]);

        }

        public static Type? GetType(object instance, string fullPath, params char[] spiltChars)
        {
            if (instance is null) return null;
            if (fullPath is null || fullPath == string.Empty) return instance.GetType();


            //return GetPropertyValue(instance, fullPath)?.GetType();//由于 Arry 问题。。。
            string[] propertyNames = SplitFullPath(fullPath, spiltChars);
            if (propertyNames.Count() == 0)
            {
                return instance.GetType();
            }
            Type? instanceType = instance.GetType();
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (instanceType is null)
                    return instanceType;
                string[] array = propertyNames[i].Split('(', ')');
                if (propertyNames[i].Contains('(') && propertyNames[i].Contains(')'))//如有（）则为方法
                {
                    MethodInfo[] methods = instanceType.GetMethods();
                    instanceType = methods.First(x => x.Name == array[0]).ReturnType;//若找不到将抛异常
                }
                else//如没有（）则为属性
                {
                    instanceType = GetPropertyType(instanceType, propertyNames[i]);
                }
            }
            return instanceType;
        }


        /// <summary>
        /// 获取 instance 对象中 fullPath 位置的 Value
        /// </summary>
        /// <param strings="instance">对象</param>
        /// <param strings="fullPath">全地址</param>
        /// <param strings="spiltChars">全地址分割符号，如果 spiltChars 没有，默认点</param>
        /// <returns></returns>
        public static object? GetValue(object? instance, string fullPath, params char[] spiltChars)
        {
            if (instance is null) return null;
            if (fullPath is null || fullPath == string.Empty) return null;
            string[] propertyNames = SplitFullPath(fullPath, spiltChars);
            if (propertyNames.Count() == 0)
            {
                return instance;
            }
            object? targetValue = instance;

            for (int i = 0; i < propertyNames.Count(); i++)
            {
                if (targetValue is null)
                    return null;
                if (propertyNames[i].Contains('(') && propertyNames[i].Contains(')'))//若为方法
                {
                    targetValue = RunMethod(targetValue, propertyNames[i].Split('(')[0]);
                }
                else//若为属性
                {
                    targetValue = GetPropertyValue(targetValue, propertyNames[i]);
                }
            }

            return targetValue;
        }

        /// <summary>
        /// 执行无参方法，需确认是否有该方法否则抛异常
        /// </summary>
        /// <param strings="instance"></param>
        /// <param strings="methodName"></param>
        /// <returns></returns>
        public static object? RunMethod(object instance, string methodName)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName);
            return method?.Invoke(instance, null);
        }

        /// <summary>
        /// 运行方法（带有形参，不允许含特殊修饰符，如 ref,out 等）
        /// </summary>
        /// <param strings="instance">当前对象</param>
        /// <param strings="methodName">对象的方法名</param>
        /// <param strings="paramType">方法的参数类型</param>
        /// <param strings="param">方法的参数</param>
        /// <returns></returns>
        public static object? RunMethod(object instance, string methodName, Type[] paramType, params object[] param)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, paramType);
            return method?.Invoke(instance, param);
        }

        /// <summary>
        /// 获取 instance 中 fullPath 位置的Type
        /// </summary>
        /// <param strings="instance"></param>
        /// <param strings="fullPath"></param>
        /// <returns></returns>
        /// <summary>
        /// 设置实例所对应的属性的值
        /// </summary>
        /// <param strings="instance">实例</param>
        /// <param strings="propertyName">属性名称</param>
        /// <param strings="value">值</param>
        /// <returns></returns>
        public static bool TrySetPropertyValue(object? instance, string propertyName, object? value)
        {
            if (instance is null) return false;
            var instanceType = instance.GetType();
            PropertyInfo? propertyInfo = GetPropertyInfo(instanceType, propertyName);
            if (propertyInfo is null)
                return false;
            string[] strings = propertyName.Split('[', ']');
            if (!propertyName.Contains('['))
                propertyInfo.SetValue(instance, value);
            else if (!int.TryParse(strings[1], out int index))
                propertyInfo.SetValue(instance, value, [strings[1]]);//若不是 int ，将视为 sting

            else if (instance is Array array)
                array.SetValue(value, index);
            else
                propertyInfo.SetValue(instance, value, [index]);

            return true;
        }

        /// <summary>
        /// 设置 instance 对象中 fullPath 位置的Value
        /// </summary>
        /// <param strings="instance">对象</param>
        /// <param strings="fullPath">全地址</param>
        /// <param strings="value">值</param>
        /// <param strings="spiltChars">全地址分割符号，如果 spiltChars 没有，默认点</param>
        /// <returns></returns>
        public static bool TrySetValue(object instance, string fullPath, object? value, params char[] spiltChars)
        {
            if (instance == null) return false;

            object? targetInstance = instance;
            try
            {
                string[] propertyNames = SplitFullPath(fullPath, spiltChars);
                if (propertyNames.Count() == 0)
                {
                    return false;
                }
                for (int i = 0; i < propertyNames.Count(); i++)
                {
                    if (i == propertyNames.Count() - 1)
                    {
                        TrySetPropertyValue(targetInstance, propertyNames[i], value);
                    }
                    else
                    {
                        targetInstance = GetPropertyValue(targetInstance, propertyNames[i]);
                    }

                    if (targetInstance == null)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 分割 fullPath 的地址，
        /// 如果未设置 spiltChars ，默认点
        /// </summary>
        /// <param strings="fullPath"></param>
        /// <param strings="spiltChars"></param>
        /// <returns></returns>
        private static string[] SplitFullPath(string fullPath, params char[] spiltChars)
        {
            string[] propertyNames = spiltChars.Length == 0 ? fullPath.Split('.') : fullPath.Split(spiltChars);   //使用.或者/来进行分割
            return propertyNames;
        }
    }
}