using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jankiele
{
    public class JankielLoader
    {
        public static string LoadText(string url) => System.IO.File.ReadAllText(url);
        public static IEnumerable<Tuple<double, double>> ParseJankielFile(string text)
        =>
            text.Split('\n')
                .Skip(1)
                .Select(line => line
                    .Split(' ')
                    .Take(2)
                    .Select(numberText => double.Parse(numberText)))
                .Select(coordinates =>
                    new Tuple<double, double>(coordinates.First(), coordinates.Last()));

        public static IEnumerable<JankielPerson> CreateJankiels(IEnumerable<Tuple<double, double>> coordinates)
        {
            // select 10 for more optimal solution
            var random = new Random(0);
            var alreadyUsedIDs = new HashSet<int>();
            var pool = int.MaxValue;
            int getNewID()
            {
                var id = random.Next(pool);
                while (alreadyUsedIDs.Contains(id))
                    id = random.Next(pool);
                alreadyUsedIDs.Add(id);
                return id;
            }
            return coordinates.Select(coords => new JankielPerson(coords, getNewID(), random.Next()));
        }
    }
}
