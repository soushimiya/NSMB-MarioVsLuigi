using NSMB.Entities;

public interface IFireballInteractable {

    bool InteractWithFireball(FireballMover fireball);

    bool InteractWithIceball(FireballMover iceball);

}
