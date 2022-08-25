namespace OSCLeashNet
{
    public class LeashParameters
    {
        public bool WasGrabbed = false;
        public bool Grabbed = false;
        public float Stretch = 0;
        public float ZPositive = 0;
        public float ZNegative = 0;
        public float XPositive = 0;
        public float XNegative = 0;

        public override string ToString()
        {
            return
                $"Grabbed - {Grabbed} was {WasGrabbed} | Stretch - {Stretch} | ({ZPositive - ZNegative}, {XPositive - XNegative})";
        }
    }
}