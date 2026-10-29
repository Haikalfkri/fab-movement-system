using ok1.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace ok1.Services
{
    public class GroupMappingOption
    {
        private readonly ok1Context db = new ok1Context();


        public async Task<(List<string> areaList, List<string> areaDIList)> GetAreaByGroup(string groupName)
        {
            var areaList = new List<string>();
            var areaDIList = new List<string>();

            if (!string.IsNullOrEmpty(groupName))
            {
                var mappings = await db.GroupMapping
                    .Where(g => g.GroupName == groupName)
                    .ToListAsync();

                //areaList = mappings.Select(m => m.AreaDI).Distinct().OrderBy(m => m).ToList();
                //areaDIList = mappings.Select(m => m.Area).Distinct().OrderBy(m => m).ToList();

                areaList = mappings.Select(m => m.Area).Distinct().OrderBy(m => m).ToList();
                areaDIList = mappings.Select(m => m.AreaDI).Distinct().OrderBy(m => m).ToList();
            }

            return (areaList, areaDIList);
        }
    }
}