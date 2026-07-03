using System;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public class DroneRuntime
    {
        public DroneDefinition Definition { get; }

        public DroneRuntime(DroneDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
