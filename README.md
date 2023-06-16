# GodOfWarWindSimulation
战神4的风场系统，使用Unity Compute Shader和ECS两种系统复刻实现
原知乎：https://zhuanlan.zhihu.com/p/524594823
发完文章之后想起可以用ECS来代替Compute Shader中复杂的计算，虽然效率差了点但是就可以绕开Compute Shader从而能在移动端快乐玩耍了
其实这个东西很早就做完了，但是因为工作（拧螺丝）太忙，加上有别的个人私事所以这个东西就被我忘记了（理直气壮
现在整理了一些注释发出来，这只是我当初用来学Compute Shader和ECS写的一个demo，如果有一些写得很丑陋的地方还希望理解
环境：Unity 2021.3.7f1
必须得有：
![image](https://github.com/SaberZG/GodOfWarWindSimulation/assets/74618371/24d907c3-a839-4d26-9a63-68792d59f76e)
可能不打算在新的ECS系统下重写ECS的部分，如果重写了我可能会发出来，但请不要抱太大的期待（x
![image](https://github.com/SaberZG/GodOfWarWindSimulation/assets/74618371/dc3cf479-bb82-48ce-84fd-ed8ad264cda8)
（当初写的有点赶，在不同的实现方式下，需要修改这部分的代码）
