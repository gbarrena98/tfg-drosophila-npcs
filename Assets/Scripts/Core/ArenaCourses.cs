using System;

namespace NpcLab.Core
{
    // Playable courses have their own identities and never enter the development pilot.
    public static class ArenaCourses
    {
        public static TrialDefinition Challenge(ScenarioKind scenario)
        {
            var d = new TrialDefinition { schemaVersion = "2", datasetRole = "showcase-challenge",
                scenario = (int)scenario, start = new Vec2(0,-8), index = -1 };
            if (scenario == ScenarioKind.Obstacles)
            {
                d.configurationId = "B-SLALOM";
                d.initialHeading = 90;
                d.goal = d.secondGoal = new Vec2(0,8);
                d.obstacles = new[] {
                    new CircleObstacle(0,new Vec2(0,-3.8),1), new CircleObstacle(1,new Vec2(1,-.5),1.5),
                    new CircleObstacle(2,new Vec2(-1.1,2.6),1.45), new CircleObstacle(3,new Vec2(1.2,5.3),1),
                    CircleObstacle.Rectangle(4,new Vec2(-4,4.2),2.8,.8,28),
                    CircleObstacle.Rectangle(5,new Vec2(4,-3.5),2.8,.8,-28)
                };
            }
            else if (scenario == ScenarioKind.MovingGoal)
            {
                d.configurationId = "C-RELEVO";
                d.goal = new Vec2(0,6.5); d.secondGoal = new Vec2(.8,-6.5); d.thirdGoal = new Vec2(4.5,7);
                d.goalChangeSeconds = 10; d.secondChangeSeconds = 16;
            }
            else throw new ArgumentException("Elige un circuito B o C.");
            d.Validate();
            return d;
        }

        public static TrialDefinition EmptyWorkshop() => new TrialDefinition {
            schemaVersion = "2", configurationId = "B-TALLER", datasetRole = "free-workshop",
            scenario = (int)ScenarioKind.Obstacles, index = -1, start = new Vec2(0,-8),
            goal = new Vec2(0,8), secondGoal = new Vec2(0,8), initialHeading = 0
        };

        public static TrialDefinition RelayWorkshop()
        {
            var relay = Challenge(ScenarioKind.MovingGoal);
            relay.configurationId = "C-TALLER";
            relay.datasetRole = "free-workshop";
            return relay;
        }
    }
}
