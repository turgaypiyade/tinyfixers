public enum ClearAnimationMode
{
    Default,
    LightningStrike,
    GoalFlyToHud,
    // Mini Elevator / Servis Asansörü booster: asansör sütunu alttan yukarı tararken
    // geçtiği her (temizlenebilir) taşı sıra ile sağa/sola savurur. Obstacle'lar
    // savrulmaz — normal hasarını alır (shouldClearTile==false yolunda atlanır).
    ElevatorLift
}
