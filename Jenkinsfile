pipeline {
  agent any
  options { timestamps(); disableConcurrentBuilds() }
  triggers { pollSCM('H/5 * * * *') }
  stages {
    stage('Build API image') {
      steps { sh 'docker build -t ecommerce-api:local .' }
    }
    stage('Compile-check gateway') {
      steps { sh 'docker build -t webhook-gateway:ci webhook-gateway' }
    }
    stage('Deploy') {
      steps {
        sh '''
          cd /home/ala/ecommerce/deploy
          docker compose up -d --no-deps api
          sleep 20
          docker compose restart nginx
        '''
      }
    }
    stage('Smoke test: local API') {
      steps {
        sh '''
          for i in 1 2 3 4 5 6; do
            code=$(docker run --rm --network deploy_default curlimages/curl:latest -sk -o /dev/null -w "%{http_code}" https://nginx/api/products || true)
            echo "attempt $i: $code"
            [ "$code" = "200" ] && exit 0
            sleep 10
          done
          exit 1
        '''
      }
    }
    stage('Smoke test: cloud gateway') {
      steps { sh 'curl -sf -m 90 https://skinet-webhook-gateway.onrender.com/health' }
    }
  }
}
