import re

with open('./Assets/_Project/Scripts/HectonBoidController.cs', 'r') as f:
    text = f.read()

text = text.replace('public int BoidCount => boidCount;', '''public int BoidCount => boidCount;

        /// <summary>
        /// Pure logic redirect for boid alignment force.
        /// Extracts calculation safely for tests.
        /// </summary>
        public static UnityEngine.Vector3 CalculateSteerForce(UnityEngine.Vector3 boidVelocity, UnityEngine.Vector3 averageNeighborVelocity, float maxSteerForce)
        {
            var systemBoidVel = new System.Numerics.Vector3(boidVelocity.x, boidVelocity.y, boidVelocity.z);
            var systemAvgVel = new System.Numerics.Vector3(averageNeighborVelocity.x, averageNeighborVelocity.y, averageNeighborVelocity.z);
            var result = Hecton8.PureLogic.Ecosystem.FlockingBoidAlignmentVector.Calculate(systemBoidVel, systemAvgVel, maxSteerForce);
            return new UnityEngine.Vector3(result.X, result.Y, result.Z);
        }
''')

with open('./Assets/_Project/Scripts/HectonBoidController.cs', 'w') as f:
    f.write(text)
