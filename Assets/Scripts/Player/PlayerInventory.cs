using UnityEngine;

// Unity wrapper that gives the player an Inventory.
public class PlayerInventory : MonoBehaviour
{
    private Inventory items;

    // Created on first use so other components can read it during their own Awake.
    public Inventory Items => items ??= new Inventory();
}
