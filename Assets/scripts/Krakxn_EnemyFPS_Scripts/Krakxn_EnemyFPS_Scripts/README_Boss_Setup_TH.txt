วิธีใช้ BossEnemyController

ไฟล์ในชุดนี้:
1. BossEnemyController.cs
2. BossProjectile.cs
3. PlayerHealth.cs เวอร์ชันเพิ่ม Auto Recovery

Setup Boss:
- สร้าง GameObject Boss
- ใส่ CharacterController
- ใส่ EnemyHealth ถ้าต้องการให้ Boss มีเลือด/ตายได้
- ใส่ BossEnemyController
- ตั้ง Layer Player เป็น Player
- ที่ BossEnemyController > Player Layer เลือก Player เท่านั้น
- สร้าง Empty child ชื่อ FirePoint แล้วลากใส่ช่อง Fire Point

Flow Boss:
- Melee 3 ครั้ง
- Charge 2 ครั้ง
- Shoot 5 นัด
- Cooldown
- วน Loop ใหม่

Warning:
- Melee = วงแดงบนพื้นก่อนทำดาเมจ
- Charge = กรอบสี่เหลี่ยมบนพื้นตามทิศทางที่จะพุ่ง
- Shoot = เส้นแดงก่อนยิง

ค่าแนะนำ:
Melee Warning Time = 0.8
Charge Warning Time = 0.9
Shoot Warning Time = 0.45
Cycle Cooldown = 2
Charge Distance = 13
Charge Width = 2.8
Charge Speed = 18

PlayerHealth Recovery:
- Enable Health Recovery = true
- Recovery Delay After Damage = 5
- Recovery Per Second = 8
- Recover To Full Health = true

หมายเหตุ:
- ถ้าวง/กรอบแดงไม่ติดพื้น ให้ตั้ง Ground Layer เป็น Layer ของพื้น เช่น Ground
- พื้นต้องมี Collider
- Player ต้องมี PlayerHealth และ Layer Player
