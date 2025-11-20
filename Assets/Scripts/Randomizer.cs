using UnityEngine;
public class Randomizer
{
      string currWeapon = "";
    public void Randomize()
    {
        Randomize rndwpn = new Randomize();
        int weaponNum = rndwpn.Next(1, 5);

        if (weaponNum = 1)
        {
            currWeapon = "Sword";
        }
        else if (weaponNum = 2)
        {
            currWeapon = "Pistol";
        }
        else if (weaponNum = 3)
        {
            currWeapon = "Shotgun";
        
        }
        else if (weaponNum = 4)
        {
            currWeapon = "RPG";
        }
        
    }
}
