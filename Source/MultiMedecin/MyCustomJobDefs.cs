using Verse;
using RimWorld;

namespace MultiDoctorSurgery
{
    [DefOf]
    public static class MyCustomJobDefs
    {
        public static JobDef AssistSurgeryLoop;

        static MyCustomJobDefs()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MyCustomJobDefs));
        }
    }
}
