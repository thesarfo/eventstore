using System.Runtime.CompilerServices;

namespace eventstore_net.Util;

public static class ObjectFactory<T>
{
    public static T GetEmpty() =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}