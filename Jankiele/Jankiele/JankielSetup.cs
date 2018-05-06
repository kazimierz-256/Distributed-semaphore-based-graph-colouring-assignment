using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jankiele
{
    public class JankielSetup
    {
        public const double maximumJankielHearingDistance = 3.000001d;
        public static double EuclideanDistance(
            Tuple<double, double> position1,
            Tuple<double, double> position2)
            => Math.Sqrt(
                (position1.Item1 - position2.Item1)
                * (position1.Item1 - position2.Item1)
                + (position1.Item2 - position2.Item2)
                * (position1.Item2 - position2.Item2));

        public static IEnumerable<Thread> PrepareJankiels(IEnumerable<JankielPerson> jankiels)
        => jankiels.Select(jankiel => new Thread(() =>
            {
                jankiel.AddNeighbours(jankiels.Where(jankielPerson =>
                {
                    var distance = EuclideanDistance(jankielPerson.GetCoordinates(), jankiel.GetCoordinates());
                    return 0 < distance && distance <= maximumJankielHearingDistance;
                }));
                jankiel.Launch();
            }));

    }
}
