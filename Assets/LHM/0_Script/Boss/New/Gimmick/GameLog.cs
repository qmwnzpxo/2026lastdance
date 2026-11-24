using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace UnMachine
{
    /// <summary>
    /// 프로젝트 공용 로그. 에디터·개발 빌드에서만 컴파일되고 릴리스 빌드에서는 호출 자체가 제거된다.
    /// 새 코드는 Debug.Log 대신 이 클래스를 쓴다 (AI_RULES 5장).
    /// </summary>
    public static class GameLog
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevBuildSymbol = "DEVELOPMENT_BUILD";

        /// <summary>일반 정보 로그.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void Info(string message, Object context = null)
        {
            Debug.Log(message, context);
        }

        /// <summary>경고 로그.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void Warn(string message, Object context = null)
        {
            Debug.LogWarning(message, context);
        }

        /// <summary>에러 로그. 인스펙터 참조 누락 등으로 기능을 끌 때 쓴다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void Error(Object context, string message)
        {
            Debug.LogError(message, context);
        }

        /// <summary>예외 로그.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void LogException(Exception exception, Object context = null)
        {
            Debug.LogException(exception, context);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 릴리스 빌드에서는 등록하지 않는다: GameLog 호출이 제거되므로 핸들러가 있으면 예외가 조용히 사라진다.
        // 등록하지 않으면 UniTask 기본 동작(Debug.LogException)이 그대로 남는다.

        /// <summary>
        /// 씬 로드 전 1회 실행: .Forget() 한 UniTask에서 처리되지 않은 예외를 GameLog로 모은다 (AI_RULES 5장).
        /// 별도 부트스트랩 오브젝트를 씬에 배치하지 않아도 되게 정적 초기화로 둔다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterUniTaskExceptionHandler()
        {
            // 도메인 리로드를 꺼둔 경우 중복 구독을 막기 위해 먼저 해제한다.
            UniTaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            UniTaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            Info("[GameLog] 초기화: UniTask 미관측 예외 → GameLog 등록");
        }

        private static void OnUnobservedTaskException(Exception exception)
        {
            LogException(exception);
        }
#endif
    }
}
