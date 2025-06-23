using UnityEngine;
using System.Collections.Generic;

namespace AVBDPhysics
{
  [System.Serializable]
  public struct Particle
  {
      public Vector2 position;
      public Vector2 velocity;
      public Vector2 inertialPosition;
      public float invMass;
      
      public Particle(Vector2 pos, float mass)
      {
          position = pos;
          velocity = Vector2.zero;
          inertialPosition = pos;
          invMass = mass > 0 ? 1.0f / mass : 0.0f;
      }
  }

  [System.Serializable]
  public struct LinkConstraint
  {
      public int particleA;
      public int particleB;
      public float restLength;
      public float stiffness;
      public float lambda; // For warmstarting
      
      public LinkConstraint(int a, int b, float length, float k = 1.0f)
      {
          particleA = a;
          particleB = b;
          restLength = length;
          stiffness = k;
          lambda = 0.0f;
      }
  }

  public struct EnergyDerivatives
  {
      public Vector2 gradient;
      public Matrix2x2 hessian;
      
      public EnergyDerivatives(Vector2 grad, Matrix2x2 hess)
      {
          gradient = grad;
          hessian = hess;
      }
  }

  [System.Serializable]
  public struct Matrix2x2
  {
      public float m00, m01, m10, m11;
      
      public Matrix2x2(float _m00, float _m01, float _m10, float _m11)
      {
          m00 = _m00; m01 = _m01;
          m10 = _m10; m11 = _m11;
      }
      
      public static Matrix2x2 Identity => new Matrix2x2(1, 0, 0, 1);
      
      public static Matrix2x2 operator +(Matrix2x2 a, Matrix2x2 b)
      {
          return new Matrix2x2(
              a.m00 + b.m00, a.m01 + b.m01,
              a.m10 + b.m10, a.m11 + b.m11
          );
      }
      
      public static Matrix2x2 operator *(Matrix2x2 m, float s)
      {
          return new Matrix2x2(m.m00 * s, m.m01 * s, m.m10 * s, m.m11 * s);
      }
      
      public float Determinant => m00 * m11 - m01 * m10;
      
      public Matrix2x2 Inverse
      {
          get
          {
              float det = Determinant;
              if (Mathf.Abs(det) < 1e-7f) return Identity;
              
              float invDet = 1.0f / det;
              return new Matrix2x2(
                  m11 * invDet, -m01 * invDet,
                  -m10 * invDet, m00 * invDet
              );
          }
      }
      
      public static Vector2 operator *(Matrix2x2 m, Vector2 v)
      {
          return new Vector2(
              m.m00 * v.x + m.m01 * v.y,
              m.m10 * v.x + m.m11 * v.y
          );
      }
  }

  public class AVBDSolver : MonoBehaviour
  {
      [Header("AVBD Parameters")]
      [SerializeField] private float beta = 1.0f;
      [SerializeField] private float minStiffness = 10f;
      [SerializeField] private float maxStiffness = 100000f;
      [SerializeField] private float maxLambda = 10000f;
      [SerializeField] private int solverIterations = 10;
      [SerializeField] private bool enableWarmstarting = true;
      
      [Header("Physics")]
      [SerializeField] private Vector2 gravity = new Vector2(0, -9.81f);
      [SerializeField] private float damping = 0.99f;
      
      private List<Particle> particles = new List<Particle>();
      private List<LinkConstraint> constraints = new List<LinkConstraint>();
      private Dictionary<int, List<int>> constraintGraph = new Dictionary<int, List<int>>();
      
      // Warmstarting data
      private List<Vector2> previousLambdas = new List<Vector2>();
      
      public void AddParticle(Vector2 position, float mass = 1.0f)
      {
          particles.Add(new Particle(position, mass));
          constraintGraph[particles.Count - 1] = new List<int>();
      }
      
      public void AddConstraint(int particleA, int particleB, float? restLength = null)
      {
          if (particleA >= particles.Count || particleB >= particles.Count) return;
          
          float length = restLength ?? Vector2.Distance(particles[particleA].position, particles[particleB].position);
          int constraintIndex = constraints.Count;
          
          constraints.Add(new LinkConstraint(particleA, particleB, length));
          constraintGraph[particleA].Add(constraintIndex);
          constraintGraph[particleB].Add(constraintIndex);
          
          // Initialize warmstarting data
          previousLambdas.Add(Vector2.zero);
      }
      
      private EnergyDerivatives GetParticleConstraintDerivatives(Vector2 position, int particleIndex)
      {
          Vector2 gradient = Vector2.zero;
          Matrix2x2 hessian = new Matrix2x2(0, 0, 0, 0);
          
          if (!constraintGraph.ContainsKey(particleIndex)) 
              return new EnergyDerivatives(gradient, hessian);
          
          foreach (int constraintIndex in constraintGraph[particleIndex])
          {
              var constraint = constraints[constraintIndex];
              int otherParticle = (constraint.particleA == particleIndex) ? constraint.particleB : constraint.particleA;
              
              Vector2 otherPos = particles[otherParticle].position;
              Vector2 diff = position - otherPos;
              float currentLength = diff.magnitude;
              
              if (currentLength < 1e-7f) continue;
              
              float restLength = constraint.restLength;
              float stiffness = Mathf.Clamp(constraint.stiffness, minStiffness, maxStiffness);
              
              // Apply beta parameter for AVBD acceleration
              float effectiveStiffness = stiffness * beta;
              
              // Energy gradient (first derivative)
              float lengthError = currentLength - restLength;
              Vector2 direction = diff / currentLength;
              Vector2 constraintGrad = effectiveStiffness * lengthError * direction;
              
              gradient += constraintGrad;
              
              // Energy hessian (second derivative)
              float factor1 = effectiveStiffness / currentLength;
              float factor2 = effectiveStiffness * lengthError / (currentLength * currentLength * currentLength);
              
              Matrix2x2 constraintHessian = new Matrix2x2(
                  factor1 - factor2 * diff.x * diff.x,
                  -factor2 * diff.x * diff.y,
                  -factor2 * diff.x * diff.y,
                  factor1 - factor2 * diff.y * diff.y
              );
              
              hessian += constraintHessian;
          }
          
          return new EnergyDerivatives(gradient, hessian);
      }
      
      private void ApplyWarmstarting(float deltaTime)
      {
          if (!enableWarmstarting) return;
          
          for (int i = 0; i < constraints.Count; i++)
          {
              var constraint = constraints[i];
              Vector2 lambdaGuess = previousLambdas[i];
              
              if (lambdaGuess.magnitude > 1e-7f)
              {
                  // Apply warmstarting impulse
                  Vector2 posA = particles[constraint.particleA].position;
                  Vector2 posB = particles[constraint.particleB].position;
                  Vector2 diff = posA - posB;
                  
                  if (diff.magnitude > 1e-7f)
                  {
                      Vector2 direction = diff.normalized;
                      float impulse = Vector2.Dot(lambdaGuess, direction) * 0.8f; // Damping factor
                      
                      var particleA = particles[constraint.particleA];
                      var particleB = particles[constraint.particleB];
                      
                      if (particleA.invMass > 0)
                      {
                          particleA.position += direction * impulse * particleA.invMass * deltaTime;
                          particles[constraint.particleA] = particleA;
                      }
                      
                      if (particleB.invMass > 0)
                      {
                          particleB.position -= direction * impulse * particleB.invMass * deltaTime;
                          particles[constraint.particleB] = particleB;
                      }
                  }
              }
          }
      }
      
      private void ProjectVBD(float deltaTime)
      {
          // Store lambda values for warmstarting
          List<Vector2> currentLambdas = new List<Vector2>();
          
          for (int i = 0; i < particles.Count; i++)
          {
              var particle = particles[i];
              
              if (particle.invMass <= 0.0f)
              {
                  particle.position = particle.inertialPosition;
                  particles[i] = particle;
                  continue;
              }
              
              EnergyDerivatives derivatives = GetParticleConstraintDerivatives(particle.position, i);
              
              float mdt2 = 1.0f / Mathf.Max(1e-7f, deltaTime * deltaTime * particle.invMass);
              Vector2 inertialForce = -(particle.position - particle.inertialPosition) * mdt2;
              Vector2 constraintForce = -derivatives.gradient;
              Vector2 totalForces = inertialForce + constraintForce;
              
              Matrix2x2 inertialHessian = Matrix2x2.Identity * mdt2;
              Matrix2x2 totalHessian = inertialHessian + derivatives.hessian;
              
              if (totalHessian.Determinant > 0.0f)
              {
                  Vector2 delta = totalHessian.Inverse * totalForces;
                  
                  // Clamp delta to prevent explosions
                  float deltaLength = delta.magnitude;
                  if (deltaLength > maxLambda * deltaTime)
                  {
                      delta = delta.normalized * maxLambda * deltaTime;
                  }
                  
                  particle.position += delta;
                  
                  // Store lambda for warmstarting (approximation)
                  Vector2 lambda = constraintForce * deltaTime;
                  currentLambdas.Add(lambda);
              }
              else
              {
                  currentLambdas.Add(Vector2.zero);
              }
              
              particles[i] = particle;
          }
          
          // Update warmstarting data
          if (enableWarmstarting && currentLambdas.Count == particles.Count)
          {
              for (int i = 0; i < Mathf.Min(previousLambdas.Count, currentLambdas.Count); i++)
              {
                  previousLambdas[i] = Vector2.Lerp(previousLambdas[i], currentLambdas[i], 0.1f);
              }
          }
      }
      
      public void Step(float deltaTime)
      {
          // Update inertial positions (explicit Euler integration)
          for (int i = 0; i < particles.Count; i++)
          {
              var particle = particles[i];
              
              if (particle.invMass > 0.0f)
              {
                  particle.velocity += gravity * deltaTime;
                  particle.velocity *= damping;
                  particle.inertialPosition = particle.position + particle.velocity * deltaTime;
              }
              else
              {
                  particle.inertialPosition = particle.position;
              }
              
              particles[i] = particle;
          }
          
          // Apply warmstarting if enabled
          ApplyWarmstarting(deltaTime);
          
          // AVBD constraint projection iterations
          for (int iteration = 0; iteration < solverIterations; iteration++)
          {
              ProjectVBD(deltaTime);
          }
          
          // Update velocities based on position changes
          for (int i = 0; i < particles.Count; i++)
          {
              var particle = particles[i];
              
              if (particle.invMass > 0.0f)
              {
                  particle.velocity = (particle.position - (particle.inertialPosition - particle.velocity * deltaTime)) / deltaTime;
              }
              
              particles[i] = particle;
          }
      }
      
      void FixedUpdate()
      {
          Step(Time.fixedDeltaTime);
      }
      
      void OnDrawGizmos()
      {
          if (particles == null) return;
          
          // Draw particles
          Gizmos.color = Color.red;
          foreach (var particle in particles)
          {
              Gizmos.DrawSphere(particle.position, 0.1f);
          }
          
          // Draw constraints
          Gizmos.color = Color.blue;
          foreach (var constraint in constraints)
          {
              if (constraint.particleA < particles.Count && constraint.particleB < particles.Count)
              {
                  Gizmos.DrawLine(particles[constraint.particleA].position, particles[constraint.particleB].position);
              }
          }
      }
      
      // Public accessors
      public Vector2 GetParticlePosition(int index) => particles[index].position;
      public void SetParticlePosition(int index, Vector2 position)
      {
          var particle = particles[index];
          particle.position = position;
          particles[index] = particle;
      }
      
      public int ParticleCount => particles.Count;
      public int ConstraintCount => constraints.Count;
      
      // Parameter adjustment methods
      public void SetBeta(float newBeta) => beta = Mathf.Max(0.1f, newBeta);
      public void SetStiffnessRange(float min, float max)
      {
          minStiffness = Mathf.Max(0.1f, min);
          maxStiffness = Mathf.Max(minStiffness, max);
      }
  }
}