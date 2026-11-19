Player-Entity Interaction

Common to all A,B,C

1. Secret  
   * Secret is a special shrine that when reached ends the level with victory.  
   * Adds one star point to the final score. (Final score contains the max 3 stars iff both bonus shrines and secret are found.)  
       
2. Bonus Shrine  
   * This is a bonus shrine that can be interacted which grants a permanent buff to movement speed or vision range (only one shrine per type exist).  
   * Interacting adds one star point to final score.

3. Shrine  
   * Flowers are to be offered at Shrines by interacting with shrine.   
   * Let n=3 flowers be needed to interact with Shrine.   
   * There is a Shrine network that connects them to the final secret. The network is a randomly generated directed-acyclic graph where all paths lead to the secret.  
   * Offering flowers will enable to use clues to find path to the next Shrine in the network.  
4. Flower  
   * All can pick flowers from flower plants.   
5. Fruit  
   * Player can press the interaction button to pick up fallen fruit from ground   
   * Player can consume fruit to gain some health and a temporary movement speed boost  
6. Bush  
   * Player can walk into bush and press interaction button to hide

**A (Attack, Heal)**

7. Tree  
* Player can’t get fruits from tree


8. Stone  
* Player can press interaction button to sharpen the blade with a cooldown \-\> damage double for some time


9. Animal1 (Squirrel)  
* Player can attack the squirrel to kill  
* Squirrel can try to escape  
* Player can press interaction button to cook the killed squirrel  
* Cooked squirrel becomes a consumable


10. Animal2 (Mountain Lion)  
* Player can attack the mountain lion to kill  
* Mountain lion will attack and chase the player


11. Person1 (Pilgrim)  
* Player can press the interaction button to offer a fruit to the pilgrim (only once)  
* Pilgrim will give a clue point to the player (only once)  
* If player attacks near pilgrim, pilgrim gets scared and runs away


12. Person2 (Monk)  
* Player can press the interaction button to offer a fruit to the monk (only once)  
* Monk will give a bonus clue (only once)  
* Player can’t attack monk

**B (Invisible, Dash)**

1. Tree  
* Player can’t get fruits from tree  
* If Player stay longer under/near tree he starts to take damage from fear and seeing the tree attack him (this is all in his mind but the damage is real)  
* Player can try to escape  
* Player can’t attack tree

2. Stone  
* Player can walk near the stone (large enough) and press the interaction button to hide


3. Animal1 (Squirrel)  
* Squirrel will become larger and try to attack the player if he stay near too long  
* Player can try to escape  
* Player can’t attack squirrel


4. Animal2 (Mountain Lion)  
* Mountain lion will attack the player  
* Player can try to escape  
* Player can’t attack mountain lion


5. Person1 (Pilgrim)  
* Player can press the interaction button to offer a fruit to the pilgrim (only once)  
* Pilgrim will give a clue point to the player (only once)  
* Player can stay longer without fear around pilgrims


  


6. Person2 (Monk)  
* Player can press the interaction button to offer a fruit to the monk (only once)  
* Monk will give a bonus clue (only once)  
* Player can stay longer without fear around monks

**C (Sense, Improve Vision Range)**   
**C is slower than others**

1. Tree  
* Player can press the interaction button to get fruit from tree (only once)


2. Stone  
* Player can press the interaction button to climb on a large stone  
* If player climbs on the stone, player's vision range will improve a little bit


3. Animal1 (Squirrel)  
* Player can press interaction button to catch squirrel  
* Player can use squirrel as a food for Animal2 (Mountain lion)


4. Animal2 (Mountain Lion)  
* Mountain lion can attack the player  
* Player can try to escape  
* Player can’t attack the mountain lion  
* Player can press interaction button to feed the mountain lion meat (only once)  
* If the mountain lion is fed meat it will stop/slow down


5. Person1 (Pilgrim)  
* Player can press the interaction button to offer fruits to the pilgrim (only once)  
* Pilgrim will give a clue point to the player (only once)


6. Person2 (Monk)  
* Player can press interaction button to offer fruits to the monk (only once)  
* Monk will give a bonus clue (only once)