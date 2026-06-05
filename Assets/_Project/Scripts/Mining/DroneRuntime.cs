using System;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public class DroneRuntime
    {
        public DroneDefinition Definition  { get; }
        public int             CargoAmount { get; private set; }
        public bool            IsCargoFull => CargoAmount >= Definition.CargoCap;

        public DroneRuntime(DroneDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public int AddCargo(int amount)
        {
            int space  = Definition.CargoCap - CargoAmount;
            int actual = Math.Min(amount, space);
            CargoAmount += actual;
            return actual;
        }

        public int EmptyCargo()
        {
            int hauled  = CargoAmount;
            CargoAmount = 0;
            return hauled;
        }
    }
}
