
using System;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
#endif

// ReSharper disable once CheckNamespace
namespace Library
{
#if UNITY_EDITOR
    /// <summary>
    /// 도메인 리로드 시 싱글톤 인스턴스를 초기화하는 유틸리티
    /// ISingleton을 구현하는 모든 클래스의 _instance 필드를 null로 설정하여 도메인 리로드 후 새로운 인스턴스가 생성되도록 함
    /// </summary>
    internal static class ReloadDomainSingleton
    {
        // 싱글톤 인스턴스를 저장하는 정적 필드명
        private const string InstanceFieldName = "_instance";

        // Reflection 바인딩 플래그
        private const BindingFlags InstanceFieldBindingFlags = BindingFlags.Static | BindingFlags.NonPublic;

        /// <summary>
        /// 도메인 리로드 시 호출되는 초기화 메서드
        /// ISingleton을 구현하는 모든 싱글톤 클래스의 인스턴스를 초기화함
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadDomain()
        {
            GetSingletonTypes()
                .ForEach(ResetSingletonInstance);
        }

        /// <summary>
        /// ISingleton을 구현하고 구체적인 클래스인 모든 타입을 조회
        /// 제네릭 타입 정의(Singleton<T> 같은 오픈 제네릭)는 제외
        /// </summary>
        /// <returns>ISingleton을 구현하는 구체적인 클래스 타입 목록</returns>
        private static List<Type> GetSingletonTypes()
        {
            return TypeCache.GetTypesDerivedFrom<ISingleton>()
                .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
                .ToList();
        }

        /// <summary>
        /// 싱글톤 타입의 _instance 필드를 null로 초기화
        /// ISingleton을 구현하는 타입 또는 그 상속 체인에서 _instance 필드를 찾아 null로 설정함
        /// </summary>
        /// <param name="singletonType">초기화할 싱글톤 타입</param>
        private static void ResetSingletonInstance(Type singletonType)
        {
            // 현재 타입부터 상위 타입까지 순회하며 _instance 필드를 찾음
            Type currentType = singletonType;

            while (currentType != null && currentType != typeof(object))
            {
                FieldInfo instanceField = currentType.GetField(
                    InstanceFieldName,
                    InstanceFieldBindingFlags
                );

                if (instanceField != null)
                {
                    try
                    {
                        instanceField.SetValue(null, null);
                        return; // 필드를 찾아 초기화했으면 종료
                    }
                    catch (Exception exception)
                    {
                        DebugUtil.LogError(
                            $"싱글톤 인스턴스 초기화 실패 [타입: {singletonType.FullName}]\n{exception.Message}"
                        );
                        return;
                    }
                }

                currentType = currentType.BaseType;
            }
        }
    }
#endif

    /// <summary>
    /// 싱글톤 클래스를 나타내는 마커 인터페이스
    /// </summary>
    public interface ISingleton
    {
    }

    public abstract class Singleton<T> : ISingleton, IDisposable where T : new()
    {
        #region Fields

        private static readonly object Lock = new();

        protected static T _instance;

        #endregion

        #region Properties

        public static T Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                lock (Lock)
                {
                    _instance ??= new T();
                }

                return _instance;
            }
        }

        #endregion

        //-------------- internal, public Methods --------------//
        public static bool IsCreated()
        {
            return _instance != null;
        }

        public virtual void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        //-------------- protected, private Methods --------------//
        static Singleton()
        {
        }

        protected Singleton()
        {
        }

        protected virtual void ReleaseUnmanagedResources()
        {

        }
    }
}